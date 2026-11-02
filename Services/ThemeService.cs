using System.Windows;
using NovaNet.Models;

namespace NovaNet.Services;

/// <summary>Manages application theme (Light/Dark/System).</summary>
public sealed class ThemeService
{
    readonly SettingsService _settings;
    readonly ResourceDictionary _lightResources = new() { Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml") };
    readonly ResourceDictionary _darkResources = new() { Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml") };

    public ThemeMode CurrentTheme => _settings.Settings.Theme;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
        _settings.SettingsChanged += OnSettingsChanged;
        ApplyTheme(_settings.Settings.Theme);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    void OnSettingsChanged(AppSettings s) => ApplyTheme(s.Theme);

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _settings.Settings.Theme == ThemeMode.System)
            ApplySystemTheme();
    }

    public void SetTheme(ThemeMode theme)
    {
        _settings.UpdateTheme(theme);
        ApplyTheme(theme);
    }

    void ApplyTheme(ThemeMode theme)
    {
        if (theme == ThemeMode.System) ApplySystemTheme();
        else ApplyExplicitTheme(theme);
    }

    void ApplySystemTheme()
    {
        var isDark = IsSystemDarkTheme();
        ApplyExplicitTheme(isDark ? ThemeMode.Dark : ThemeMode.Light);
    }

    void ApplyExplicitTheme(ThemeMode theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var dict = app.Resources.MergedDictionaries;
        dict.Clear();

        // Always load the base theme (DarkTheme.xaml has all the colors defined)
        dict.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml") });

        if (theme == ThemeMode.Light)
        {
            // Override with light theme colors
            dict.Add(_lightResources);
        }
    }

    static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}