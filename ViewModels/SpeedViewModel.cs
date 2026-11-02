using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the Speed/Bufferbloat test page.</summary>
public sealed partial class SpeedViewModel : ViewModelBase
{
    CancellationTokenSource? _speedCts;

    [ObservableProperty]
    private string _downloadSpeed = "–";

    [ObservableProperty]
    private string _uploadSpeed = "–";

    [ObservableProperty]
    private string _idlePing = "–";

    [ObservableProperty]
    private string _grade = "–";

    [ObservableProperty]
    private Brush _gradeBrush = Pal.Muted;

    [ObservableProperty]
    private string _detailText = "Grades: A+ adds under 5 ms when busy, A under 30, B under 60, C under 200. If you land on B or worse, see the Router guide for the QoS fix.";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = "Ready";

    public SpeedViewModel() { }

    [RelayCommand]
    async Task StartTestAsync()
    {
        if (IsBusy) return;
        _speedCts = new CancellationTokenSource();

        await ExecuteAsync(async () =>
        {
            ResetResults();
            ProgressText = "Measuring idle latency...";
            ProgressValue = 0;

            var prog = new Progress<(string Phase, double Mbps, double? Ping)>(p =>
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    ProgressText = $"{p.Phase}: {p.Mbps:0} Mbps" + (p.Ping.HasValue ? $" • ping {p.Ping:0} ms" : "");
                    ProgressValue = p.Mbps > 0 ? Math.Min(100, p.Mbps / 1000 * 100) : 0;
                });
            });

            var result = await LoadTest.RunAsync(prog, _speedCts.Token);

            DownloadSpeed = $"{result.DownMbps:0}";
            UploadSpeed = $"{result.UpMbps:0}";
            IdlePing = $"{result.IdleMs:0}";
            Grade = result.Grade;
            GradeBrush = result.Bloat < 30 ? Pal.Mint : result.Bloat < 60 ? Pal.Amber : Pal.Coral;
            DetailText = $"Ping rose from {result.IdleMs:0} ms to {result.DownLoadedMs:0} ms during download and {result.UpLoadedMs:0} ms during upload, " +
                         $"so a busy line adds about {result.Bloat:0} ms. " +
                         (result.Bloat < 30 ? "Nothing to fix." : "Enable QoS on the router and cap it at about 90-95% of your line speed.");
            ProgressText = "Done";
            ProgressValue = 100;
        }, "Running speed test...", true);
    }

    [RelayCommand]
    void CancelTest()
    {
        _speedCts?.Cancel();
        ProgressText = "Cancelled";
    }

    void ResetResults()
    {
        DownloadSpeed = "–";
        UploadSpeed = "–";
        IdlePing = "–";
        Grade = "–";
        GradeBrush = Pal.Muted;
        DetailText = "Grades: A+ adds under 5 ms when busy, A under 30, B under 60, C under 200. If you land on B or worse, see the Router guide for the QoS fix.";
        ProgressValue = 0;
    }

    public override void Dispose()
    {
        _speedCts?.Cancel();
        _speedCts?.Dispose();
        base.Dispose();
    }
}