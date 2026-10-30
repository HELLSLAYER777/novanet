using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NovaNet.Services;

namespace NovaNet;

public partial class MainWindow : Window
{
    readonly ObservableCollection<TargetVm> targets = new();
    readonly ObservableCollection<TweakVm> tweakVms = new();
    readonly ObservableCollection<DnsVm> dnsVms = new();
    readonly PingMonitor monitor = new();
    int bestMtu;
    CancellationTokenSource? speedCts;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        TargetList.ItemsSource = targets;
        TweakList.ItemsSource = tweakVms;
        DnsList.ItemsSource = dnsVms;

        SourceInitialized += (_, _) =>
        {
            int dark = 1; // dark title bar
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        };
        Loaded += OnLoaded;
        Closed += (_, _) => { monitor.Stop(); speedCts?.Cancel(); };
        BuildGuide();
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var t in Tweaks.All) tweakVms.Add(new TweakVm(t, Store.Data.Applied.Contains(t.Id)));

        var gw = NetInfo.Gateway();
        if (gw != null) targets.Add(new TargetVm("Your router", gw, true));
        targets.Add(new TargetVm("Cloudflare 1.1.1.1", "1.1.1.1", false));
        targets.Add(new TargetVm("Google 8.8.8.8", "8.8.8.8", false));

        monitor.Sample += (host, ms) => Dispatcher.InvokeAsync(() =>
        {
            targets.FirstOrDefault(t => t.Host == host)?.Add(ms);
            UpdateVerdict();
        });
        monitor.Start(targets.Select(t => t.Host));

        LinkInfo.Text = NetInfo.Describe();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        timer.Tick += (_, _) => LinkInfo.Text = NetInfo.Describe();
        timer.Start();
    }

    // ---------- navigation ----------

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageGuide == null) return; // still building the window
        var tag = (string)((RadioButton)sender).Tag;
        PageLive.Visibility = tag == "Live" ? Visibility.Visible : Visibility.Collapsed;
        PageOptimize.Visibility = tag == "Optimize" ? Visibility.Visible : Visibility.Collapsed;
        PageDns.Visibility = tag == "Dns" ? Visibility.Visible : Visibility.Collapsed;
        PageNet.Visibility = tag == "Net" ? Visibility.Visible : Visibility.Collapsed;
        PageSpeed.Visibility = tag == "Speed" ? Visibility.Visible : Visibility.Collapsed;
        PageGuide.Visibility = tag == "Guide" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- live ----------

    void UpdateVerdict()
    {
        var net = targets.Where(t => !t.IsLocal && t.HasData).ToList();
        if (net.Count == 0) { Verdict.Text = "Measuring…"; return; }
        double avg = net.Average(t => t.Avg), jit = net.Average(t => t.Jitter), loss = net.Average(t => t.Loss);
        string word =
            loss >= 3 ? "Packet loss is hurting your connection." :
            avg < 20 && jit < 4 ? "Excellent. Ideal for gaming and calls." :
            avg < 40 ? "Good. Fine for almost everything." :
            avg < 80 ? "Average. Games will feel a little late." : "High delay. Check the Router guide.";
        Verdict.Text = $"{word}  {avg:0} ms to the internet, jitter {jit:0.#} ms, loss {loss:0}%.";
    }

    // ---------- optimize ----------

    void Log(string msg)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            LogBox.ScrollToEnd();
        });
    }

    void SetBusy(bool busy)
    {
        BtnSafe.IsEnabled = BtnAll.IsEnabled = BtnUndo.IsEnabled = !busy;
    }

    async Task SetTweak(TweakVm vm, bool on)
    {
        vm.Busy = true;
        try
        {
            var msg = on ? await vm.T.Apply() : await vm.T.Revert();
            if (on) Store.Data.Applied.Add(vm.T.Id); else Store.Data.Applied.Remove(vm.T.Id);
            Store.Save();
            vm.IsOn = on;
            Log($"{(on ? "Applied" : "Undone")}: {vm.T.Title}. {msg}");
        }
        catch (Exception ex)
        {
            vm.IsOn = !on;
            Log($"Failed: {vm.T.Title}. {ex.Message}");
        }
        finally { vm.Busy = false; }
    }

    async void Tweak_Click(object sender, RoutedEventArgs e)
    {
        var cb = (CheckBox)sender;
        var vm = (TweakVm)cb.DataContext;
        await SetTweak(vm, cb.IsChecked == true);
    }

    async Task RunBatch(bool advanced)
    {
        SetBusy(true);
        Log("Creating a Windows restore point…");
        var rp = await Sh.PS("Checkpoint-Computer -Description 'NovaNet' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'ok'", 120000);
        Log(rp.Output.Contains("ok") ? "Restore point created." : "Restore point skipped (System Protection is off, or one was made in the last 24 h).");
        foreach (var vm in tweakVms.Where(v => advanced || !v.T.Advanced).ToList())
            if (!vm.IsOn) await SetTweak(vm, true);
        Log("Finished. Restart Windows to complete the TCP and IPv4 changes.");
        SetBusy(false);
    }

    async void OptimizeSafe_Click(object sender, RoutedEventArgs e) => await RunBatch(false);

    async void OptimizeAll_Click(object sender, RoutedEventArgs e)
    {
        var ok = MessageBox.Show(
            "Ultra mode also changes your network card settings (the link will drop for a few seconds), prefers IPv4 and switches the power plan.\n\nEverything can be undone. Continue?",
            "NovaNet", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok == MessageBoxResult.Yes) await RunBatch(true);
    }

    async void UndoAll_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        foreach (var vm in tweakVms.Where(v => v.IsOn).ToList()) await SetTweak(vm, false);
        Log("Everything is back to how it was.");
        SetBusy(false);
    }

    // ---------- DNS ----------

    async void DnsRace_Click(object sender, RoutedEventArgs e)
    {
        DnsBtn.IsEnabled = false;
        dnsVms.Clear();
        try
        {
            var results = await DnsBench.RunAsync(new Progress<string>(s => DnsStatus.Text = s));
            var ranked = results.OrderBy(r => r.Score ?? 9999).ToList();
            double best = ranked.FirstOrDefault(r => r.Score != null)?.Score ?? 1;
            foreach (var r in ranked)
            {
                bool first = r == ranked[0] && r.Score != null;
                dnsVms.Add(new DnsVm
                {
                    Name = r.Name,
                    Servers = r.Servers,
                    Score = r.Score == null ? "no reply" : $"{r.Score:0} ms",
                    Detail = r.Score == null ? "" : $"cached {r.CachedMs:0}, cold {r.ColdMs:0}",
                    Badge = first ? "fastest" : "",
                    BarWidth = r.Score == null ? 0 : Math.Max(6, 300 * best / Math.Max(best, r.Score.Value))
                });
            }
            DnsStatus.Text = "Done. Press Use on the winner.";
        }
        catch (Exception ex) { DnsStatus.Text = ex.Message; }
        finally { DnsBtn.IsEnabled = true; }
    }

    async void DnsUse_Click(object sender, RoutedEventArgs e)
    {
        var vm = (DnsVm)((FrameworkElement)sender).DataContext;
        DnsStatus.Text = "Applying…";
        DnsStatus.Text = await DnsApply.Set(vm.Servers);
        LinkInfo.Text = NetInfo.Describe();
    }

    async void DnsReset_Click(object sender, RoutedEventArgs e)
    {
        DnsStatus.Text = await DnsApply.Reset();
        LinkInfo.Text = NetInfo.Describe();
    }

    // ---------- MTU and routes ----------

    async void MtuFind_Click(object sender, RoutedEventArgs e)
    {
        MtuBtn.IsEnabled = false;
        MtuApply.IsEnabled = false;
        MtuResult.Text = "Testing…";
        var mtu = await MtuFinder.FindAsync(new Progress<string>(s => MtuResult.Text = s));
        if (mtu == null) MtuResult.Text = "Could not measure. Your router or ISP may be blocking ICMP.";
        else
        {
            bestMtu = mtu.Value;
            MtuResult.Text = mtu.Value switch
            {
                1492 => "Best MTU: 1492. That is the normal PPPoE value, so your line behaves as expected.",
                1500 => "Best MTU: 1500. Your ISP supports full-size packets over PPPoE.",
                _ => $"Best MTU: {mtu.Value}. Different from the usual 1492, so applying it avoids fragmentation."
            };
            MtuApply.IsEnabled = true;
        }
        MtuBtn.IsEnabled = true;
    }

    async Task SetMtu(int mtu)
    {
        var a = NetInfo.Active();
        if (a == null) { MtuResult.Text = "No active adapter found."; return; }
        var r = await Sh.Netsh($"interface ipv4 set subinterface \"{a.Name}\" mtu={mtu} store=persistent");
        MtuResult.Text = r.Code == 0 ? $"MTU {mtu} applied to {a.Name}." : r.Output;
    }

    async void MtuApply_Click(object sender, RoutedEventArgs e) => await SetMtu(bestMtu);
    async void MtuReset_Click(object sender, RoutedEventArgs e) => await SetMtu(1500);

    async Task Tool(Func<Task<string>> f, string busy)
    {
        RouteOut.Text = busy;
        RouteOut.Text = await f();
    }

    async void RouteScan_Click(object sender, RoutedEventArgs e) => await Tool(Fix.RouteReport, "Scanning routes and tracing the path… about 10 seconds.");
    async void FlushDns_Click(object sender, RoutedEventArgs e) => await Tool(Fix.FlushDns, "Working…");
    async void FlushArp_Click(object sender, RoutedEventArgs e) => await Tool(Fix.FlushArpAndRoutes, "Working…");
    async void RenewIp_Click(object sender, RoutedEventArgs e) => await Tool(Fix.RenewIp, "Renewing. The connection drops for a moment.");
    async void ResetStack_Click(object sender, RoutedEventArgs e) => await Tool(Fix.ResetStack, "Working…");

    // ---------- speed ----------

    async void Speed_Click(object sender, RoutedEventArgs e)
    {
        SpeedBtn.IsEnabled = false;
        SpDown.Text = SpUp.Text = SpIdle.Text = SpGrade.Text = "–";
        speedCts = new CancellationTokenSource();
        var prog = new Progress<(string Phase, double Mbps, double? Ping)>(p =>
            SpeedStatus.Text = p.Mbps > 0
                ? $"{p.Phase}: {p.Mbps:0} Mbps" + (p.Ping.HasValue ? $", ping {p.Ping:0} ms" : "")
                : p.Phase + "…");
        try
        {
            var r = await LoadTest.RunAsync(prog, speedCts.Token);
            SpDown.Text = r.DownMbps.ToString("0");
            SpUp.Text = r.UpMbps.ToString("0");
            SpIdle.Text = r.IdleMs.ToString("0");
            SpGrade.Text = r.Grade;
            SpGrade.Foreground = r.Bloat < 30 ? Pal.Mint : r.Bloat < 60 ? Pal.Amber : Pal.Coral;
            SpDetail.Text =
                $"Ping rose from {r.IdleMs:0} ms to {r.DownLoadedMs:0} ms during download and {r.UpLoadedMs:0} ms during upload, " +
                $"so a busy line adds about {r.Bloat:0} ms. " +
                (r.Bloat < 30 ? "Nothing to fix." : "Enable QoS on the Tenda and cap it at about 90 to 95% of your line speed.");
            SpeedStatus.Text = "Done.";
        }
        catch (Exception ex) { SpeedStatus.Text = "Test failed: " + ex.Message; }
        finally { SpeedBtn.IsEnabled = true; }
    }

    // ---------- guide ----------

    void BuildGuide()
    {
        var items = new (string Title, string Body)[]
        {
            ("Keep the VSOL in true bridge mode",
             "The VSOL should only pass the fiber signal. No routing, no NAT, no DHCP, no Wi-Fi. Only the Tenda dials PPPoE. If the Route doctor reports double NAT, this is the cause."),
            ("Set the PPPoE MTU on the Tenda",
             "On the Tenda's WAN settings for PPPoE, set MTU to the value the MTU finder reports (normally 1492). Leave MSS clamping on if the firmware offers it."),
            ("Put the DNS winner on the router",
             "Run the DNS race here, then enter the two winning addresses in the Tenda's DNS settings. Every phone and TV in the house gets the faster lookups."),
            ("Use a cable for the PC",
             "600 Mbps fits easily on a gigabit port. Wi-Fi adds jitter even when the speed looks great. Use Cat5e or better."),
            ("Wi-Fi 7 devices",
             "Use 5 GHz or 6 GHz with an 80 MHz channel width. Wider channels collect more interference and often raise delay."),
            ("Fix bufferbloat with QoS",
             "If the Speed page grades you B or worse, turn on the Tenda's QoS or bandwidth control and set download and upload to about 90 to 95% of your measured speed."),
            ("IPv6",
             "If your provider does not give you IPv6, switch it off on the router's LAN side, or use the 'Prefer IPv4 over IPv6' tweak here, so devices stop waiting for it."),
            ("What no software can change",
             "If the ping to your router is under 1 ms but 1.1.1.1 is slow, the delay is upstream: your provider's route or the fiber line. Send them the Route doctor output and ask them to check the path.")
        };
        foreach (var (title, body) in items)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("Panel"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            sp.Children.Add(new TextBlock
            {
                Text = body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0),
                Foreground = (Brush)FindResource("Muted"), LineHeight = 20
            });
            card.Child = sp;
            GuidePanel.Children.Add(card);
        }
    }
}
