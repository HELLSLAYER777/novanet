using CommunityToolkit.Mvvm.ComponentModel;
using NovaNet.Models;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for a DNS benchmark result row.</summary>
public sealed partial class DnsResultViewModel : ObservableObject
{
    public string Name { get; }
    public string[] Servers { get; }
    public string ServersText => string.Join(", ", Servers);
    public string Score { get; }
    public string Detail { get; }
    public string Badge { get; }
    public double BarWidth { get; }

    public DnsResultViewModel(string name, string[] servers, double? score, string detail, string badge, double barWidth)
    {
        Name = name;
        Servers = servers;
        Score = score.HasValue ? $"{score.Value:0.##} ms" : "–";
        Detail = detail;
        Badge = badge;
        BarWidth = barWidth;
    }
}