using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Windows;

namespace NovaNet.ViewModels;

/// <summary>Main application ViewModel coordinating all pages.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    readonly SettingsService _settings;
    readonly ThemeService _theme;
    readonly SettingsWindow _settingsWindow;

    [ObservableProperty]
    private string _currentPage = "Live";

    [ObservableProperty]
    private bool _isDarkTheme;

    public LiveViewModel LiveVm { get; }
    public OptimizeViewModel OptimizeVm { get; }
    public DnsViewModel DnsVm { get; }
    public NetViewModel NetVm { get; }
    public SpeedViewModel SpeedVm { get; }
    public GuideViewModel GuideVm { get; }

    public SettingsService Settings => _settings;
    public ThemeService Theme => _theme;

    public MainViewModel(SettingsService settings, ThemeService theme,
        LiveViewModel liveVm, OptimizeViewModel optimizeVm, DnsViewModel dnsVm,
        NetViewModel netVm, SpeedViewModel speedVm, GuideViewModel guideVm,
        SettingsWindow settingsWindow)
    {
        _settings = settings;
        _theme = theme;
        _settingsWindow = settingsWindow;
        LiveVm = liveVm;
        OptimizeVm = optimizeVm;
        DnsVm = dnsVm;
        NetVm = netVm;
        SpeedVm = speedVm;
        GuideVm = guideVm;

        IsDarkTheme = _theme.CurrentTheme == ThemeMode.Dark || 
                      (_theme.CurrentTheme == ThemeMode.System && ThemeService.IsSystemDarkThemeStatic());
    }

    [RelayCommand]
    void Navigate(string page)
    {
        CurrentPage = page;
    }

    [RelayCommand]
    void ToggleTheme()
    {
        var modes = Enum.GetValues<ThemeMode>();
        int current = (int)_theme.CurrentTheme;
        int next = (current + 1) % modes.Length;
        _theme.SetTheme(modes[next]);
        IsDarkTheme = modes[next] == ThemeMode.Dark || 
                      (modes[next] == ThemeMode.System && ThemeService.IsSystemDarkThemeStatic());
    }

    [RelayCommand]
    void SetTheme(ThemeMode mode)
    {
        _theme.SetTheme(mode);
        IsDarkTheme = mode == ThemeMode.Dark || 
                      (mode == ThemeMode.System && ThemeService.IsSystemDarkThemeStatic());
    }

    [RelayCommand]
    void OpenSettings()
    {
        _settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
        _settingsWindow.ShowDialog();
    }

    [RelayCommand]
    void MinimizeToTray()
    {
        // Handled by MainWindow
    }

    public void Initialize()
    {
        LiveVm.Initialize();
    }

    public override void Dispose()
    {
        LiveVm.Dispose();
        SpeedVm.Dispose();
        base.Dispose();
    }
}

public static class ThemeServiceExtensions
{
    public static bool IsSystemDarkThemeStatic()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }
}