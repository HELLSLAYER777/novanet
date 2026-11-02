using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the Live monitoring page.</summary>
public sealed partial class LiveViewModel : ViewModelBase
{
    readonly PingMonitor _monitor = new();
    readonly SettingsService _settings;
    readonly ObservableCollection<TargetViewModel> _targets = new();
    readonly ObservableCollection<LatencyHistoryEntry> _history = new();
    readonly object _historyLock = new();

    public ObservableCollection<TargetViewModel> Targets => _targets;
    public ObservableCollection<LatencyHistoryEntry> History => _history;

    [ObservableProperty]
    private string _verdict = "Measuring...";

    [ObservableProperty]
    private Brush _verdictBrush = Pal.Muted;

    [ObservableProperty]
    private string _linkInfo = string.Empty;

    public LiveViewModel(SettingsService settings)
    {
        _settings = settings;
        _monitor.Sample += OnSample;
    }

    [RelayCommand]
    public void Initialize()
    {
        var gw = NetInfo.Gateway();
        if (gw != null) _targets.Add(new TargetViewModel("Your router", gw, true));
        _targets.Add(new TargetViewModel("Cloudflare 1.1.1.1", "1.1.1.1", false));
        _targets.Add(new TargetViewModel("Google 8.8.8.8", "8.8.8.8", false));

        _monitor.Start(_targets.Select(t => t.Host));
        LinkInfo = NetInfo.Describe();
    }

    [RelayCommand]
    public void RefreshLinkInfo() => LinkInfo = NetInfo.Describe();

    [RelayCommand]
    public void AddCustomTarget((string Name, string Host) tuple)
    {
        var (name, host) = tuple;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host)) return;
        if (_targets.Any(t => t.Host == host)) return;

        _targets.Add(new TargetViewModel(name, host, false));
        _monitor.Start(new[] { host });
    }

    [RelayCommand]
    public void RemoveTarget(TargetViewModel? target)
    {
        if (target == null) return;
        _monitor.Stop();
        _targets.Remove(target);
        _monitor.Start(_targets.Select(t => t.Host));
    }

    [RelayCommand]
    public void ExportHistoryCsv()
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Timestamp,Host,LatencyMs,Status");
        
        lock (_historyLock)
        {
            foreach (var entry in _history)
            {
                csv.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.Host},{entry.LatencyMs ?? 0},{(entry.LatencyMs.HasValue ? "OK" : "Lost")}");
            }
        }

        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovaNet", $"latency_history_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, csv.ToString());
        
        System.Windows.MessageBox.Show($"History exported to:\n{path}", "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _history.Clear();
        }
    }

    void OnSample(string host, double? ms)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var target = _targets.FirstOrDefault(t => t.Host == host);
            target?.Add(ms);
            UpdateVerdict();

            // Record to history
            lock (_historyLock)
            {
                _history.Add(new LatencyHistoryEntry(DateTime.Now, host, ms));
                
                // Trim old entries based on retention setting
                var cutoff = DateTime.Now.AddHours(-_settings.Settings.HistoryRetentionHours);
                while (_history.Count > 0 && _history[0].Timestamp < cutoff)
                    _history.RemoveAt(0);
            }
        });
    }

    void UpdateVerdict()
    {
        var net = _targets.Where(t => !t.IsLocal && t.HasData).ToList();
        if (net.Count == 0) { Verdict = "Measuring..."; VerdictBrush = Pal.Muted; return; }

        double avg = net.Average(t => t.Avg);
        double jit = net.Average(t => t.Jitter);
        double loss = net.Average(t => t.Loss);

        string word;
        if (loss >= 3) word = "Packet loss is hurting your connection.";
        else if (avg < 20 && jit < 4) word = "Excellent. Ideal for gaming and calls.";
        else if (avg < 40) word = "Good. Fine for almost everything.";
        else if (avg < 80) word = "Average. You may feel lag in fast games.";
        else word = "High latency. Consider the Optimize tab.";

        Verdict = word;
        VerdictBrush = loss >= 3 ? Pal.Coral : avg < 40 ? Pal.Mint : Pal.Amber;
    }

    public override void Dispose()
    {
        _monitor.Stop();
        base.Dispose();
    }
}

public sealed record LatencyHistoryEntry(DateTime Timestamp, string Host, double? LatencyMs);