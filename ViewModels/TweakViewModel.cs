using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for a tweak item.</summary>
public sealed partial class TweakViewModel : ObservableObject
{
    public Tweak Tweak { get; }

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private bool _busy;

    public bool CanToggle => !Busy;

    public string Title => Tweak.Title;
    public string Note => Tweak.Note;
    public string Tag => Tweak.Advanced ? "Advanced" : "Safe";
    public Brush TagBrush => Tweak.Advanced ? Pal.Amber : Pal.Mint;

    public TweakViewModel(Tweak tweak, bool on)
    {
        Tweak = tweak;
        _isOn = on;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (Busy) return;
        _ = ToggleAsync(value);
    }

    [RelayCommand]
    async Task ToggleAsync(bool enable)
    {
        Busy = true;
        try
        {
            string result = enable ? await Tweak.Apply() : await Tweak.Revert();
            // Store handles persistence automatically
        }
        catch (Exception ex)
        {
            IsOn = !enable; // Revert UI on failure
            System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            Busy = false;
        }
    }
}