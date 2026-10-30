using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using NovaNet.Services;

namespace NovaNet;

public static class Pal
{
    static SolidColorBrush B(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
    public static readonly SolidColorBrush Mint = B("#5BE3B0");
    public static readonly SolidColorBrush Amber = B("#FFB547");
    public static readonly SolidColorBrush Coral = B("#FF6B6B");
    public static readonly SolidColorBrush Muted = B("#8A94B0");
}

public abstract class Vm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One live ping card on the Live page.</summary>
public sealed class TargetVm : Vm
{
    const int MaxSamples = 60;
    readonly List<double> values = new();
    public string Name { get; }
    public string Host { get; }
    public bool IsLocal { get; }

    string msText = "…"; Brush msBrush = Pal.Muted; string stats = "waiting for first reply"; double[] series = Array.Empty<double>();
    public string MsText { get => msText; set => Set(ref msText, value); }
    public Brush MsBrush { get => msBrush; set => Set(ref msBrush, value); }
    public string Stats { get => stats; set => Set(ref stats, value); }
    public double[] Series { get => series; set => Set(ref series, value); }

    public double Avg { get; private set; }
    public double Jitter { get; private set; }
    public double Loss { get; private set; }
    public bool HasData { get; private set; }

    public TargetVm(string name, string host, bool local) { Name = name; Host = host; IsLocal = local; }

    public void Add(double? ms)
    {
        values.Add(ms ?? double.NaN);
        if (values.Count > MaxSamples) values.RemoveAt(0);

        var ok = values.Where(v => !double.IsNaN(v)).ToList();
        HasData = ok.Count > 0;
        Loss = 100.0 * (values.Count - ok.Count) / values.Count;
        Avg = ok.Count > 0 ? ok.Average() : 0;
        double jit = 0; int n = 0;
        for (int i = 1; i < values.Count; i++)
            if (!double.IsNaN(values[i]) && !double.IsNaN(values[i - 1])) { jit += Math.Abs(values[i] - values[i - 1]); n++; }
        Jitter = n > 0 ? jit / n : 0;

        if (ms == null) { MsText = "lost"; MsBrush = Pal.Coral; }
        else
        {
            MsText = ms.Value < 1 ? "<1" : ms.Value.ToString("0");
            double good = IsLocal ? 3 : 25, ok2 = IsLocal ? 10 : 60;
            MsBrush = ms.Value <= good ? Pal.Mint : ms.Value <= ok2 ? Pal.Amber : Pal.Coral;
        }
        Stats = $"avg {Avg:0.#} ms, jitter {Jitter:0.#} ms, loss {Loss:0}%";
        Series = values.ToArray();
    }
}

public sealed class TweakVm : Vm
{
    public Tweak T { get; }
    bool isOn, busy;
    public TweakVm(Tweak t, bool on) { T = t; isOn = on; }
    public string Title => T.Title;
    public string Note => T.Note;
    public string Tag => T.Advanced ? "Advanced" : "Safe";
    public Brush TagBrush => T.Advanced ? Pal.Amber : Pal.Mint;
    public bool IsOn { get => isOn; set => Set(ref isOn, value); }
    public bool Busy { get => busy; set { Set(ref busy, value); Raise(nameof(CanToggle)); } }
    public bool CanToggle => !busy;
}

public sealed class DnsVm
{
    public string Name { get; init; } = "";
    public string[] Servers { get; init; } = Array.Empty<string>();
    public string ServersText => string.Join(", ", Servers);
    public string Score { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Badge { get; init; } = "";
    public double BarWidth { get; init; }
}

/// <summary>Tiny live graph: draws the last 60 samples as a glowing line. NaN means a lost packet.</summary>
public sealed class Spark : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(object), typeof(Spark),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Spark),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public object? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var v = Values as double[];
        if (v == null || v.Length < 2 || w < 4 || h < 4) return;

        var real = v.Where(d => !double.IsNaN(d)).ToList();
        double max = Math.Max(8, (real.Count > 0 ? real.Max() : 0) * 1.3);
        double dx = w / 59.0;
        double X(int i) => w - (v.Length - 1 - i) * dx;
        double Y(double val) => h - Math.Min(1, val / max) * (h - 4) - 2;

        Color c = (Stroke as SolidColorBrush)?.Color ?? Colors.Orange;
        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var lostBrush = new SolidColorBrush(Color.FromArgb(70, 255, 107, 107));
        var fill = new LinearGradientBrush(Color.FromArgb(70, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B), 90);

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var g = line.Open())
        using (var a = area.Open())
        {
            bool open = false; double startX = 0, lastX = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (double.IsNaN(v[i]))
                {
                    dc.DrawRectangle(lostBrush, null, new Rect(X(i) - 2, 0, 4, h));
                    if (open) { a.LineTo(new Point(lastX, h), false, false); a.LineTo(new Point(startX, h), false, false); open = false; }
                    continue;
                }
                var p = new Point(X(i), Y(v[i]));
                if (!open) { g.BeginFigure(p, false, false); a.BeginFigure(new Point(p.X, h), true, true); a.LineTo(p, false, false); startX = p.X; open = true; }
                else { g.LineTo(p, true, true); a.LineTo(p, false, false); }
                lastX = p.X;
            }
            if (open) { a.LineTo(new Point(lastX, h), false, false); }
        }
        line.Freeze(); area.Freeze();
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, pen, line);
    }
}
