using System.Net.NetworkInformation;

namespace NovaNet.Services;

public static class MtuFinder
{
    /// <summary>Finds the largest packet that crosses your line without fragmenting. PPPoE normally gives 1492.</summary>
    public static async Task<int?> FindAsync(IProgress<string>? prog = null)
    {
        foreach (var host in new[] { "1.1.1.1", "8.8.8.8" })
        {
            if (!await Probe(host, 1200)) continue;
            int lo = 1200, hi = 1472;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                prog?.Report($"Testing {mid + 28} bytes…");
                if (await Probe(host, mid)) lo = mid; else hi = mid - 1;
            }
            return lo + 28; // 20 bytes IP + 8 bytes ICMP
        }
        return null;
    }

    static async Task<bool> Probe(string host, int payload)
    {
        using var p = new Ping();
        var buf = new byte[payload];
        var opt = new PingOptions(64, true); // Don't Fragment
        for (int i = 0; i < 2; i++)
        {
            try
            {
                var r = await p.SendPingAsync(host, 1000, buf, opt);
                if (r.Status == IPStatus.Success) return true;
            }
            catch { }
        }
        return false;
    }
}
