using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the Guide page.</summary>
public sealed partial class GuideViewModel : ViewModelBase
{
    public ObservableCollection<GuideItem> Items { get; } = new();

    public GuideViewModel()
    {
        LoadGuide();
    }

    void LoadGuide()
    {
        var items = new (string Title, string Body)[]
        {
            ("Keep the ONT in true bridge mode",
             "The fiber ONT should only pass the fiber signal. No routing, no NAT, no DHCP, no Wi-Fi. Only your router dials PPPoE. If the Route Doctor reports double NAT, this is the cause."),
            ("Set the PPPoE MTU on the router",
             "On the router's WAN settings for PPPoE, set MTU to the value the MTU finder reports (normally 1492). Leave MSS clamping on if the firmware offers it."),
            ("Put the DNS winner on the router",
             "Run the DNS race here, then enter the two winning addresses in the router's DNS settings. Every phone and TV in the house gets the faster lookups."),
            ("Use a cable for the PC",
             "Gigabit fits easily on a wired port. Wi-Fi adds jitter even when the speed looks great. Use Cat5e or better."),
            ("Wi-Fi optimization",
             "Use 5 GHz or 6 GHz with an 80 MHz channel width. Wider channels collect more interference and often raise delay. Disable 2.4 GHz if not needed."),
            ("Fix bufferbloat with QoS",
             "If the Speed page grades you B or worse, turn on the router's QoS or bandwidth control and set download and upload to about 90-95% of your measured speed."),
            ("IPv6 considerations",
             "If your provider does not give you IPv6, switch it off on the router's LAN side, or use the 'Prefer IPv4 over IPv6' tweak in Optimize, so devices stop waiting for it."),
            ("What no software can change",
             "If the ping to your router is under 1 ms but 1.1.1.1 is slow, the delay is upstream: your provider's route or the fiber line. Send them the Route Doctor output and ask them to check the path."),
            ("Monitor continuously",
             "Keep NovaNet running in the background (minimize to tray). The Live page shows real-time latency to your router and major DNS providers. Sudden spikes indicate Wi-Fi congestion or ISP issues."),
            ("Backup before changes",
             "Every registry value and network-card option is saved to %AppData%\\NovaNet\\backup.json before it changes. \"Undo all\" in Optimize restores your exact original values.")
        };

        foreach (var (title, body) in items)
            Items.Add(new GuideItem(title, body));
    }
}

public sealed record GuideItem(string Title, string Body);