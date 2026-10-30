using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace NovaNet.Services;

public sealed record LoadResult(double DownMbps, double UpMbps, double IdleMs, double DownLoadedMs, double UpLoadedMs)
{
    public double Bloat => Math.Max(0, Math.Max(DownLoadedMs, UpLoadedMs) - IdleMs);
    public string Grade => Bloat switch { < 5 => "A+", < 30 => "A", < 60 => "B", < 200 => "C", < 400 => "D", _ => "F" };
}

/// <summary>Speed test plus bufferbloat: measures latency while your line is saturated.</summary>
public static class LoadTest
{
    const string PingHost = "1.1.1.1";

    public static async Task<LoadResult> RunAsync(IProgress<(string Phase, double Mbps, double? Ping)> prog, CancellationToken ct)
    {
        prog.Report(("Measuring idle latency", 0, null));
        var idle = await IdlePing(12);
        var (down, dl) = await Phase("Download", true, prog, ct);
        var (up, ul) = await Phase("Upload", false, prog, ct);
        return new LoadResult(down, up, idle, dl, ul);
    }

    static async Task<double> IdlePing(int count)
    {
        var v = new List<double>();
        using var p = new Ping();
        for (int i = 0; i < count; i++)
        {
            try { var r = await p.SendPingAsync(PingHost, 1000); if (r.Status == IPStatus.Success) v.Add(r.RoundtripTime); } catch { }
            await Task.Delay(120);
        }
        return Median(v);
    }

    static double Median(List<double> v)
    {
        if (v.Count == 0) return 0;
        v.Sort();
        return v[v.Count / 2];
    }

    static async Task<(double Mbps, double LoadedMs)> Phase(string name, bool down,
        IProgress<(string Phase, double Mbps, double? Ping)> prog, CancellationToken ct)
    {
        long bytes = 0;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tok = stop.Token;
        using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 16 }) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NovaNet/1.0");

        var workers = Enumerable.Range(0, 6).Select(_ => Worker(http, down, tok, n => Interlocked.Add(ref bytes, n))).ToArray();

        var pings = new List<double>();
        double? lastPing = null;
        var sw = Stopwatch.StartNew();
        var pinger = Task.Run(async () =>
        {
            using var p = new Ping();
            while (!tok.IsCancellationRequested)
            {
                try
                {
                    var r = await p.SendPingAsync(PingHost, 1500);
                    if (r.Status == IPStatus.Success)
                    {
                        lastPing = r.RoundtripTime;
                        if (sw.Elapsed.TotalSeconds > 2) lock (pings) pings.Add(r.RoundtripTime);
                    }
                }
                catch { }
                try { await Task.Delay(200, tok); } catch { return; }
            }
        });

        long b0 = 0; double t0 = 0;
        while (sw.Elapsed.TotalSeconds < 10 && !ct.IsCancellationRequested)
        {
            await Task.Delay(250);
            double t = sw.Elapsed.TotalSeconds;
            long b = Interlocked.Read(ref bytes);
            if (b0 == 0 && t >= 2) { b0 = b; t0 = t; }
            double live = b0 > 0 ? (b - b0) * 8 / Math.Max(0.001, t - t0) / 1e6 : b * 8 / Math.Max(0.001, t) / 1e6;
            prog.Report((name, live, lastPing));
        }

        double tEnd = sw.Elapsed.TotalSeconds;
        long bEnd = Interlocked.Read(ref bytes);
        stop.Cancel();
        try { await Task.WhenAll(workers); } catch { }
        try { await pinger; } catch { }

        double mbps = b0 > 0 && tEnd > t0 ? (bEnd - b0) * 8 / (tEnd - t0) / 1e6 : 0;
        double loaded;
        lock (pings) loaded = Median(pings);
        return (mbps, loaded);
    }

    static async Task Worker(HttpClient http, bool down, CancellationToken ct, Action<int> add)
    {
        var buf = new byte[65536];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (down)
                {
                    using var resp = await http.GetAsync("https://speed.cloudflare.com/__down?bytes=100000000", HttpCompletionOption.ResponseHeadersRead, ct);
                    using var s = await resp.Content.ReadAsStreamAsync(ct);
                    int n;
                    while ((n = await s.ReadAsync(buf, ct)) > 0) add(n);
                }
                else
                {
                    using var content = new CountingContent(20_000_000, add, ct);
                    using var resp = await http.PostAsync("https://speed.cloudflare.com/__up", content, ct);
                }
            }
            catch (OperationCanceledException) { return; }
            catch { try { await Task.Delay(300, ct); } catch { return; } }
        }
    }

    sealed class CountingContent : HttpContent
    {
        readonly long len; readonly Action<int> add; readonly CancellationToken ct;

        public CountingContent(long len, Action<int> add, CancellationToken ct)
        {
            this.len = len; this.add = add; this.ct = ct;
            Headers.ContentType = new("application/octet-stream");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buf = new byte[65536];
            Random.Shared.NextBytes(buf);
            long left = len;
            while (left > 0)
            {
                ct.ThrowIfCancellationRequested();
                int n = (int)Math.Min(buf.Length, left);
                await stream.WriteAsync(buf.AsMemory(0, n), ct);
                add(n);
                left -= n;
            }
        }

        protected override bool TryComputeLength(out long length) { length = len; return true; }
    }
}
