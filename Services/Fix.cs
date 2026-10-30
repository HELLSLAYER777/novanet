using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NovaNet.Services;

/// <summary>Route doctor and the repair toolbox.</summary>
public static class Fix
{
    public static async Task<string> FlushDns()
    {
        var r = await Sh.RunAsync("ipconfig.exe", "/flushdns");
        return r.Code == 0 ? "DNS cache cleared." : r.Output;
    }

    public static async Task<string> FlushArpAndRoutes()
    {
        await Sh.Netsh("interface ip delete arpcache");
        await Sh.Netsh("interface ip delete destinationcache");
        return "ARP table and destination cache cleared.";
    }

    public static async Task<string> RenewIp()
    {
        await Sh.RunAsync("ipconfig.exe", "/release");
        var r = await Sh.RunAsync("ipconfig.exe", "/renew", 30000);
        return r.Code == 0 ? "New address obtained from your router." : r.Output;
    }

    public static async Task<string> ResetStack()
    {
        await Sh.Netsh("winsock reset");
        await Sh.Netsh("int ip reset");
        return "Winsock and TCP/IP reset. Restart Windows to finish.";
    }

    public static async Task<string> RouteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("DEFAULT ROUTES (lowest total wins)");
        var r = await Sh.PS(@"
Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' | Sort-Object { $_.RouteMetric } | ForEach-Object {
  $i = Get-NetIPInterface -InterfaceIndex $_.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
  $a = Get-NetAdapter -InterfaceIndex $_.InterfaceIndex -ErrorAction SilentlyContinue
  '{0} ({1}) via {2}, total metric {3}' -f $a.Name, $a.InterfaceDescription, $_.NextHop, ($_.RouteMetric + $i.InterfaceMetric)
}");
        var routes = r.Output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        foreach (var l in routes) sb.AppendLine("  " + l);
        if (routes.Count > 1)
        {
            sb.AppendLine("  Several default routes exist. Turn on 'Prefer the wired connection' in Optimize.");
            if (routes.Any(l => Regex.IsMatch(l, "VMware|VirtualBox|Hyper-V|TAP|WireGuard|VPN|Tailscale", RegexOptions.IgnoreCase)))
                sb.AppendLine("  A virtual or VPN adapter is competing for traffic. Disconnect it if you don't need it.");
        }
        else if (routes.Count == 1) sb.AppendLine("  One default route. Good.");

        sb.AppendLine();
        sb.AppendLine("PATH TO THE INTERNET (first 8 hops to 1.1.1.1)");
        var t = await Sh.RunAsync("tracert.exe", "-d -h 8 -w 700 1.1.1.1", 40000);
        sb.AppendLine(t.Output);

        var hops = new List<IPAddress>();
        foreach (var line in t.Output.Split('\n'))
        {
            var m = Regex.Match(line, @"^\s*\d+\s.*?(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\s*$");
            if (m.Success && IPAddress.TryParse(m.Groups[1].Value, out var ip) && !ip.ToString().StartsWith("1.1.1.1"))
                hops.Add(ip);
        }
        sb.AppendLine();
        sb.AppendLine("DIAGNOSIS");
        if (hops.Count >= 2 && NetInfo.IsPrivate(hops[0]) && NetInfo.IsPrivate(hops[1]))
            sb.AppendLine("  Hop 2 is also a private address. That can mean double NAT (check that the VSOL is in true bridge mode and only the Tenda dials PPPoE), or simply that your provider uses private addresses inside its network.");
        else if (hops.Count >= 2 && NetInfo.IsCgnat(hops[1]))
            sb.AppendLine("  Your provider uses carrier-grade NAT (100.64.x.x). This is normal for some ISPs, but it blocks port forwarding.");
        else
            sb.AppendLine("  No double NAT detected. Your PPPoE session terminates on the Tenda as expected.");
        return sb.ToString();
    }
}
