using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the DNS page.</summary>
public sealed partial class DnsViewModel : ViewModelBase
{
    readonly ObservableCollection<DnsResultViewModel> _dnsResults = new();
    readonly SettingsService _settings;

    public ObservableCollection<DnsResultViewModel> DnsResults => _dnsResults;

    [ObservableProperty]
    private string _currentDns = "Detecting...";

    [ObservableProperty]
    private string _customDns1 = string.Empty;

    [ObservableProperty]
    private string _customDns2 = string.Empty;

    public DnsViewModel(SettingsService settings)
    {
        _settings = settings;
        LoadCurrentDns();
        _customDns1 = _settings.Settings.PingTargets.Length > 0 ? _settings.Settings.PingTargets[0] : "1.1.1.1";
        _customDns2 = _settings.Settings.PingTargets.Length > 1 ? _settings.Settings.PingTargets[1] : "1.0.0.1";
    }

    void LoadCurrentDns()
    {
        var dns = NetInfo.CurrentDns();
        CurrentDns = dns.Length > 0 ? string.Join(", ", dns) : "None detected";
    }

    [RelayCommand]
    async Task RunBenchmarkAsync()
    {
        await ExecuteAsync(async () =>
        {
            _dnsResults.Clear();
            StatusMessage = "Testing DNS providers...";

            var results = await DnsBench.RunAsync(new Progress<string>(msg => StatusMessage = msg));

            double? bestScore = results.Where(r => r.Score.HasValue).Min(r => r.Score);
            int rank = 0;
            foreach (var r in results.OrderBy(r => r.Score ?? double.MaxValue))
            {
                rank++;
                string badge = rank == 1 ? "🥇" : rank == 2 ? "🥈" : rank == 3 ? "🥉" : "";
                double bar = r.Score.HasValue && bestScore.HasValue && bestScore > 0
                    ? Math.Min(100, 100 * bestScore.Value / r.Score.Value)
                    : 0;

                _dnsResults.Add(new DnsResultViewModel(r.Name, r.Servers, r.Score, r.Score.HasValue ? $"avg {r.Score:0.#} ms" : "failed", badge, bar));
            }

            if (bestScore.HasValue)
                StatusMessage = $"Winner: {results.First(r => r.Score == bestScore).Name} ({bestScore:0.#} ms)";
        }, "Running DNS benchmark...");
    }

    [RelayCommand]
    async Task ApplyDnsAsync(DnsResultViewModel? result)
    {
        if (result == null) return;
        await ExecuteAsync(async () =>
        {
            string msg = await DnsApply.Set(result.Servers);
            System.Windows.MessageBox.Show(msg, "DNS Applied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadCurrentDns();
        }, $"Applying {result.Name}...");
    }

    [RelayCommand]
    async Task ApplyCustomDnsAsync()
    {
        var servers = new[] { CustomDns1, CustomDns2 }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (servers.Length == 0) return;

        await ExecuteAsync(async () =>
        {
            string msg = await DnsApply.Set(servers);
            System.Windows.MessageBox.Show(msg, "DNS Applied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadCurrentDns();
        }, "Applying custom DNS...");
    }

    [RelayCommand]
    async Task ResetDnsAsync()
    {
        await ExecuteAsync(async () =>
        {
            string msg = await DnsApply.Reset();
            System.Windows.MessageBox.Show(msg, "DNS Reset", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            LoadCurrentDns();
        }, "Resetting DNS...");
    }
}