using System.Net.NetworkInformation;

namespace NovaNet.Services;

public sealed class PingMonitor
{
    public event Action<string, double?>? Sample;
    CancellationTokenSource? cts;

    public void Start(IEnumerable<string> hosts)
    {
        Stop();
        cts = new CancellationTokenSource();
        foreach (var h in hosts.ToList()) _ = Loop(h, cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();
        cts = null;
    }

    async Task Loop(string host, CancellationToken ct)
    {
        using var p = new Ping();
        while (!ct.IsCancellationRequested)
        {
            double? ms = null;
            try
            {
                var r = await p.SendPingAsync(host, 1000);
                if (r.Status == IPStatus.Success) ms = r.RoundtripTime;
            }
            catch { }
            Sample?.Invoke(host, ms);
            try { await Task.Delay(1000, ct); } catch { return; }
        }
    }
}
