using System.Text.RegularExpressions;

namespace NovaNet.Services;

public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Note { get; init; }
    public bool Advanced { get; init; }
    public required Func<Task<string>> Apply { get; init; }
    public required Func<Task<string>> Revert { get; init; }
}

public static class Tweaks
{
    const string Tcpip = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    const string Profile = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    public static readonly List<Tweak> All = new()
    {
        new Tweak
        {
            Id = "tcp-stack",
            Title = "Low-latency TCP stack",
            Note = "Keeps receive auto-tuning on for your 600 Mbps line, turns off packet coalescing (RSC), TCP timestamps and the auto-tuning heuristic that can throttle fast links. Lower delay on every connection.",
            Apply = async () =>
            {
                var cmds = new[]
                {
                    "int tcp set global autotuninglevel=normal", "int tcp set global rss=enabled",
                    "int tcp set global rsc=disabled", "int tcp set global ecncapability=disabled",
                    "int tcp set global timestamps=disabled", "int tcp set heuristics disabled"
                };
                return await Netsh(cmds);
            },
            Revert = async () => await Netsh(new[]
            {
                "int tcp set global autotuninglevel=normal", "int tcp set global rsc=enabled",
                "int tcp set global timestamps=disabled", "int tcp set heuristics enabled"
            })
        },
        new Tweak
        {
            Id = "nagle",
            Title = "Disable Nagle and delayed ACK",
            Note = "Stops Windows from holding small packets back to batch them. Helps games, voice calls, SSH and anything that sends many small messages. Applied to every network interface.",
            Apply = () => Task.FromResult(Nagle(true)),
            Revert = () => Task.FromResult(Nagle(false))
        },
        new Tweak
        {
            Id = "throttle",
            Title = "Remove multimedia network throttling",
            Note = "Windows limits network packets per millisecond while media or games run. This removes that limit and gives foreground apps full priority.",
            Apply = () =>
            {
                Reg.SetDword("throttle", Profile, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF));
                Reg.SetDword("throttle", Profile, "SystemResponsiveness", 0);
                return Task.FromResult("throttling index set to unlimited");
            },
            Revert = () => { Reg.Restore("throttle"); return Task.FromResult("restored your original values"); }
        },
        new Tweak
        {
            Id = "qos",
            Title = "Release reserved bandwidth",
            Note = "Windows QoS can hold back part of your line for itself. This sets that reservation to 0%.",
            Apply = () =>
            {
                Reg.SetDword("qos", @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0);
                return Task.FromResult("reserved bandwidth set to 0%");
            },
            Revert = () => { Reg.Restore("qos"); return Task.FromResult("restored"); }
        },
        new Tweak
        {
            Id = "delivery-opt",
            Title = "Stop Windows Update peer-sharing",
            Note = "Prevents Windows from uploading updates to other PCs over your connection, which can add delay during games and calls.",
            Apply = () =>
            {
                Reg.SetDword("delivery-opt", @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 0);
                return Task.FromResult("peer-sharing disabled");
            },
            Revert = () => { Reg.Restore("delivery-opt"); return Task.FromResult("restored"); }
        },
        new Tweak
        {
            Id = "eth-priority",
            Title = "Prefer the wired connection",
            Note = "Routing fix: gives Ethernet the lowest route metric so Windows never sends traffic over Wi-Fi, a VPN adapter or a virtual switch by mistake when the cable is plugged in.",
            Apply = async () =>
            {
                var r = await Sh.PS(@"
$e = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.PhysicalMediaType -eq '802.3' }
foreach ($a in $e) { Set-NetIPInterface -InterfaceIndex $a.ifIndex -AutomaticMetric Disabled -InterfaceMetric 5 -ErrorAction SilentlyContinue }
$w = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.PhysicalMediaType -match '802.11|Native' }
foreach ($a in $w) { Set-NetIPInterface -InterfaceIndex $a.ifIndex -AutomaticMetric Disabled -InterfaceMetric 50 -ErrorAction SilentlyContinue }
'done'");
                return r.Output.Contains("done") ? "Ethernet metric 5, Wi-Fi metric 50" : r.Output;
            },
            Revert = async () =>
            {
                await Sh.PS("Get-NetAdapter | ForEach-Object { Set-NetIPInterface -InterfaceIndex $_.ifIndex -AutomaticMetric Enabled -ErrorAction SilentlyContinue }");
                return "metrics back to automatic";
            }
        },
        new Tweak
        {
            Id = "ipv4-first",
            Title = "Prefer IPv4 over IPv6",
            Note = "If your PPPoE line has no working IPv6, Windows can wait for IPv6 attempts to time out before falling back, which makes pages feel slow to start. This tells Windows to try IPv4 first. Restart required.",
            Advanced = true,
            Apply = () =>
            {
                Reg.SetDword("ipv4-first", @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0x20);
                return Task.FromResult("IPv4 preferred (restart Windows to finish)");
            },
            Revert = () => { Reg.Restore("ipv4-first"); return Task.FromResult("restored (restart Windows)"); }
        },
        new Tweak
        {
            Id = "nic-power",
            Title = "Network card: power saving off",
            Note = "Turns off Energy-Efficient Ethernet, Green Ethernet and selective suspend. These save a little power but can add 1 to 5 ms when the card wakes up. The connection drops for a few seconds while it applies.",
            Advanced = true,
            Apply = async () =>
            {
                var n = await NicProps("nic-power", "Energy|Green|Power Saving|Gigabit Lite|Selective Suspend|Ultra Low Power|Idle Power", "Disabled");
                await Sh.PS("Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' } | ForEach-Object { Set-NetAdapterPowerManagement -Name $_.Name -AllowComputerToTurnOffDevice Disabled -ErrorAction SilentlyContinue }");
                return n;
            },
            Revert = async () =>
            {
                var n = await RestoreNicProps("nic-power");
                await Sh.PS("Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' } | ForEach-Object { Set-NetAdapterPowerManagement -Name $_.Name -AllowComputerToTurnOffDevice Enabled -ErrorAction SilentlyContinue }");
                return n;
            }
        },
        new Tweak
        {
            Id = "interrupt-mod",
            Title = "Interrupt moderation off",
            Note = "Lets the network card interrupt the CPU for every packet instead of batching them. Lowest possible delay, at the cost of slightly higher CPU use at full speed. The connection drops for a few seconds while it applies.",
            Advanced = true,
            Apply = () => NicProps("interrupt-mod", "^Interrupt Moderation$", "Disabled"),
            Revert = () => RestoreNicProps("interrupt-mod")
        },
        new Tweak
        {
            Id = "power-plan",
            Title = "Ultimate Performance power plan",
            Note = "Stops the CPU and PCIe link from dropping into deep sleep states between packets. Best for desktops; on a laptop it costs battery.",
            Advanced = true,
            Apply = async () =>
            {
                var r = await Sh.RunAsync("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                var m = Regex.Match(r.Output, "[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
                if (!m.Success) return "this Windows edition has no Ultimate Performance plan";
                await Sh.RunAsync("powercfg.exe", "-setactive " + m.Value);
                return "plan activated";
            },
            Revert = async () =>
            {
                await Sh.RunAsync("powercfg.exe", "-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                return "Balanced plan active again";
            }
        }
    };

    // ---------- helpers ----------

    static async Task<string> Netsh(IEnumerable<string> cmds)
    {
        int ok = 0, n = 0;
        foreach (var c in cmds)
        {
            n++;
            var r = await Sh.Netsh(c);
            if (r.Code == 0) ok++;
        }
        return $"{ok} of {n} settings applied";
    }

    static string Nagle(bool on)
    {
        const string id = "nagle";
        const string root = Tcpip + @"\Interfaces\";
        using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
        int count = 0;
        if (on)
        {
            foreach (var name in k?.GetSubKeyNames() ?? Array.Empty<string>())
            {
                Reg.SetDword(id, root + name, "TcpAckFrequency", 1);
                Reg.SetDword(id, root + name, "TCPNoDelay", 1);
                count++;
            }
            return $"applied to {count} interfaces";
        }
        Reg.Restore(id);
        return "restored";
    }

    static async Task<string> NicProps(string id, string regex, string value)
    {
        var script = @"
Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
  $n = $_.Name
  Get-NetAdapterAdvancedProperty -Name $n -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match '__RX__' } | ForEach-Object {
    Write-Output ('OLD|' + $n + '|' + $_.DisplayName + '|' + $_.DisplayValue)
    try { Set-NetAdapterAdvancedProperty -Name $n -DisplayName $_.DisplayName -DisplayValue '__VAL__' -ErrorAction Stop }
    catch { Write-Output ('SKIP|' + $_.DisplayName) }
  }
}".Replace("__RX__", regex).Replace("__VAL__", value);

        var r = await Sh.PS(script, 120000);
        var lines = r.Output.Split('\n').Select(l => l.Trim()).ToList();
        if (!Store.Data.Extra.TryGetValue(id, out var saved))
        {
            saved = new List<string>();
            Store.Data.Extra[id] = saved;
        }
        foreach (var l in lines.Where(l => l.StartsWith("OLD|")))
            if (!saved.Contains(l)) saved.Add(l);
        Store.Save();
        int found = lines.Count(l => l.StartsWith("OLD|"));
        int skipped = lines.Count(l => l.StartsWith("SKIP|"));
        return found == 0 ? "your card doesn't expose this option, nothing to change"
                          : $"{found - skipped} option(s) changed";
    }

    static async Task<string> RestoreNicProps(string id)
    {
        if (!Store.Data.Extra.TryGetValue(id, out var saved) || saved.Count == 0) return "nothing to restore";
        var sb = new System.Text.StringBuilder();
        foreach (var l in saved)
        {
            var p = l.Split('|');
            if (p.Length < 4) continue;
            string q(string s) => s.Replace("'", "''");
            sb.AppendLine($"Set-NetAdapterAdvancedProperty -Name '{q(p[1])}' -DisplayName '{q(p[2])}' -DisplayValue '{q(p[3])}' -ErrorAction SilentlyContinue");
        }
        await Sh.PS(sb.ToString(), 120000);
        Store.Data.Extra.Remove(id);
        Store.Save();
        return "restored original card settings";
    }
}
