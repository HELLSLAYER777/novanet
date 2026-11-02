using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the Network/Route Doctor page.</summary>
public sealed partial class NetViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _routeReport = "Click \"Run Route Doctor\" to analyze your connection path.";

    [ObservableProperty]
    private string _mtuResult = string.Empty;

    [ObservableProperty]
    private int _bestMtu = 0;

    [ObservableProperty]
    private bool _preferWired;

    public NetViewModel() { }

    [RelayCommand]
    async Task RunRouteDoctorAsync()
    {
        await ExecuteAsync(async () =>
        {
            RouteReport = "Running route analysis...";
            string report = await Fix.RouteReport();
            RouteReport = report;
        }, "Analyzing route...");
    }

    [RelayCommand]
    async Task FindMtuAsync()
    {
        await ExecuteAsync(async () =>
        {
            MtuResult = "Finding optimal MTU...";
            int? mtu = await MtuFinder.FindAsync(new Progress<string>(msg => MtuResult = msg));
            if (mtu.HasValue)
            {
                BestMtu = mtu.Value;
                MtuResult = $"Optimal MTU: {mtu.Value} bytes (PPPoE typically 1492)";
            }
            else
            {
                MtuResult = "Could not determine MTU. Try running as Administrator.";
            }
        }, "Finding MTU...");
    }

    [RelayCommand]
    async Task ApplyMtuAsync()
    {
        if (BestMtu == 0) return;
        await ExecuteAsync(async () =>
        {
            // Apply MTU to network adapter using PowerShell
            var script = $@"
                $adapter = Get-NetAdapter -Physical | Where-Object {{ $_.Status -eq 'Up' }} | Select-Object -First 1
                if ($adapter) {{
                    Set-NetIPInterface -InterfaceIndex $adapter.InterfaceIndex -NlMtu {BestMtu}
                    'MTU set to {BestMtu} on ' + $adapter.Name
                }} else {{
                    'No active adapter found'
                }}
            ";
            var r = await Sh.PS(script);
            System.Windows.MessageBox.Show(r.Output, "MTU Applied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }, "Applying MTU...");
    }

    [RelayCommand]
    async Task FlushDnsAsync()
    {
        await ExecuteAsync(async () =>
        {
            string result = await Fix.FlushDns();
            System.Windows.MessageBox.Show(result, "DNS Flushed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });
    }

    [RelayCommand]
    async Task FlushArpAsync()
    {
        await ExecuteAsync(async () =>
        {
            string result = await Fix.FlushArpAndRoutes();
            System.Windows.MessageBox.Show(result, "ARP/Routes Flushed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });
    }

    [RelayCommand]
    async Task RenewIpAsync()
    {
        await ExecuteAsync(async () =>
        {
            string result = await Fix.RenewIp();
            System.Windows.MessageBox.Show(result, "IP Renewed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });
    }

    [RelayCommand]
    async Task ResetStackAsync()
    {
        var result = System.Windows.MessageBox.Show(
            "This will reset Winsock and TCP/IP stack. A restart is required. Continue?",
            "Reset Network Stack", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteAsync(async () =>
        {
            string msg = await Fix.ResetStack();
            System.Windows.MessageBox.Show(msg, "Stack Reset", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });
    }

    [RelayCommand]
    void CopyReport()
    {
        if (!string.IsNullOrWhiteSpace(RouteReport))
        {
            System.Windows.Clipboard.SetText(RouteReport);
            System.Windows.MessageBox.Show("Route report copied to clipboard.", "Copied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}