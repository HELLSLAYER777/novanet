using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NovaNet.Services;

public static class NetInfo
{
    /// <summary>The adapter that currently carries your internet (has an IPv4 gateway). Wired wins over Wi-Fi.</summary>
    public static NetworkInterface? Active()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Where(n => n.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
                .ThenByDescending(n => n.Speed)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    public static string? Gateway()
    {
        var a = Active();
        return a?.GetIPProperties().GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))
            ?.Address.ToString();
    }

    public static int? IfIndex(NetworkInterface? n)
    {
        try { return n?.GetIPProperties().GetIPv4Properties()?.Index; } catch { return null; }
    }

    public static string[] CurrentDns()
    {
        var a = Active();
        if (a == null) return Array.Empty<string>();
        return a.GetIPProperties().DnsAddresses
            .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
            .Select(d => d.ToString()).ToArray();
    }

    public static string Describe()
    {
        var a = Active();
        if (a == null) return "No active connection with a gateway was found.";
        var ip = a.GetIPProperties();
        var addr = ip.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
        string speed = a.Speed > 0 ? (a.Speed >= 1_000_000_000 ? $"{a.Speed / 1_000_000_000.0:0.#} Gbps" : $"{a.Speed / 1_000_000} Mbps") : "unknown speed";
        string kind = a.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Wired" :
                      a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : a.NetworkInterfaceType.ToString();
        return $"{kind} adapter: {a.Name}, link {speed}\n" +
               $"This PC: {addr}   Router: {Gateway()}\n" +
               $"DNS: {string.Join(", ", CurrentDns())}";
    }

    public static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (b.Length != 4) return false;
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }

    public static bool IsCgnat(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b.Length == 4 && b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }
}
