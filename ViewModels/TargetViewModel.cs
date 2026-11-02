using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for a single ping target card.</summary>
public sealed partial class TargetViewModel : ObservableObject
{
    const int MaxSamples = 60;
    readonly List<double> _values = new();

    public string Name { get; }
    public string Host { get; }
    public bool IsLocal { get; }

    [ObservableProperty]
    private string _msText = "…";

    [ObservableProperty]
    private Brush _msBrush = Pal.Muted;

    [ObservableProperty]
    private string _stats = "waiting for first reply";

    [ObservableProperty]
    private double[] _series = Array.Empty<double>();

    public double Avg { get; private set; }
    public double Jitter { get; private set; }
    public double Loss { get; private set; }
    public bool HasData { get; private set; }

    public TargetViewModel(string name, string host, bool local)
    {
        Name = name;
        Host = host;
        IsLocal = local;
    }

    public void Add(double? ms)
    {
        _values.Add(ms ?? double.NaN);
        if (_values.Count > MaxSamples) _values.RemoveAt(0);

        var ok = _values.Where(v => !double.IsNaN(v)).ToList();
        HasData = ok.Count > 0;
        Loss = 100.0 * (_values.Count - ok.Count) / _values.Count;
        Avg = ok.Count > 0 ? ok.Average() : 0;
        double jit = 0; int n = 0;
        for (int i = 1; i < _values.Count; i++)
            if (!double.IsNaN(_values[i]) && !double.IsNaN(_values[i - 1])) { jit += Math.Abs(_values[i] - _values[i - 1]); n++; }
        Jitter = n > 0 ? jit / n : 0;

        if (ms == null) { MsText = "lost"; MsBrush = Pal.Coral; }
        else
        {
            MsText = ms.Value < 1 ? "<1" : ms.Value.ToString("0");
            double good = IsLocal ? 3 : 25, ok2 = IsLocal ? 10 : 60;
            MsBrush = ms.Value <= good ? Pal.Mint : ms.Value <= ok2 ? Pal.Amber : Pal.Coral;
        }
        Stats = $"avg {Avg:0.#} ms, jitter {Jitter:0.#} ms, loss {Loss:0}%";
        Series = _values.ToArray();
    }
}