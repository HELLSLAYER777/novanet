using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NovaNet.Services;

public sealed record DnsResult(string Name, string[] Servers, double? CachedMs, double? ColdMs)
{
    /// <summary>Lower is better. Average of cached and cold lookups.</summary>
    public double? Score => (CachedMs, ColdMs) switch
    {
        (null, null) => null,
        (double a, null) => a,
        (null, double b) => b,
        (double a, double b) => (a + b) / 2
    };
}

public static class DnsBench
{
    static readonly (string Name, string[] Ips)[] Providers =
    {
        ("Cloudflare", new[] { "1.1.1.1", "1.0.0.1" }),
        ("Google", new[] { "8.8.8.8", "8.8.4.4" }),
        ("Quad9", new[] { "9.9.9.9", "149.112.112.112" }),
        ("OpenDNS", new[] { "208.67.222.222", "208.67.220.220" }),
        ("AdGuard", new[] { "94.140.14.14", "94.140.15.15" }),
    };

    static readonly string[] Domains = { "google.com", "youtube.com", "facebook.com", "wikipedia.org", "microsoft.com", "amazon.com" };

    public static async Task<List<DnsResult>> RunAsync(IProgress<string>? prog = null)
    {
        var list = new List<(string Name, string[] Ips)>(Providers);
        var current = NetInfo.CurrentDns();
        if (current.Length > 0) list.Insert(0, ("Current setup", current));

        var results = new List<DnsResult>();
        foreach (var (name, ips) in list)
        {
            prog?.Report($"Testing {name}…");
            var cached = new List<double>();
            var cold = new List<double>();
            foreach (var d in Domains)
            {
                await Query(ips[0], d);                       // warm the resolver cache
                var t = await Query(ips[0], d);               // popular name, answered from cache
                if (t != null) cached.Add(t.Value);
                var c = await Query(ips[0], "nn" + Random.Shared.Next(100000, 999999) + "." + d); // never seen: full lookup
                if (c != null) cold.Add(c.Value);
            }
            results.Add(new DnsResult(name, ips, Median(cached), Median(cold)));
        }
        return results;
    }

    static double? Median(List<double> v)
    {
        if (v.Count == 0) return null;
        v.Sort();
        return v[v.Count / 2];
    }

    static byte[] BuildQuery(string name, ushort id)
    {
        var b = new List<byte> { (byte)(id >> 8), (byte)(id & 0xFF), 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 };
        foreach (var label in name.Split('.'))
        {
            b.Add((byte)label.Length);
            b.AddRange(Encoding.ASCII.GetBytes(label));
        }
        b.Add(0);
        b.AddRange(new byte[] { 0, 1, 0, 1 }); // type A, class IN
        return b.ToArray();
    }

    static async Task<double?> Query(string server, string name)
    {
        try
        {
            using var udp = new UdpClient();
            var id = (ushort)Random.Shared.Next(0, 65536);
            var q = BuildQuery(name, id);
            var sw = Stopwatch.StartNew();
            await udp.SendAsync(q, q.Length, new IPEndPoint(IPAddress.Parse(server), 53));
            using var cts = new CancellationTokenSource(2000);
            var r = await udp.ReceiveAsync(cts.Token);
            sw.Stop();
            if (r.Buffer.Length > 3 && r.Buffer[0] == (byte)(id >> 8) && r.Buffer[1] == (byte)(id & 0xFF))
                return sw.Elapsed.TotalMilliseconds;
        }
        catch { }
        return null;
    }
}

public static class DnsApply
{
    public static async Task<string> Set(string[] servers)
    {
        var idx = NetInfo.IfIndex(NetInfo.Active());
        if (idx == null) return "No active adapter found.";
        var list = string.Join(",", servers.Select(s => "'" + s + "'"));
        var r = await Sh.PS($"Set-DnsClientServerAddress -InterfaceIndex {idx} -ServerAddresses ({list}); ipconfig /flushdns | Out-Null; 'ok'");
        return r.Output.Contains("ok") ? "DNS set to " + string.Join(", ", servers) : r.Output;
    }

    public static async Task<string> Reset()
    {
        var idx = NetInfo.IfIndex(NetInfo.Active());
        if (idx == null) return "No active adapter found.";
        var r = await Sh.PS($"Set-DnsClientServerAddress -InterfaceIndex {idx} -ResetServerAddresses; ipconfig /flushdns | Out-Null; 'ok'");
        return r.Output.Contains("ok") ? "DNS back to automatic (from your router)" : r.Output;
    }
}
