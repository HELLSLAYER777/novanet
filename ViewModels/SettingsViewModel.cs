using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Windows;

namespace NovaNet.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    readonly SettingsService _settings;
    readonly UpdateService _updateService;

    public AppSettings Settings => _settings.Settings;

    public SettingsViewModel(SettingsService settings, UpdateService updateService)
    {
        _settings = settings;
        _updateService = updateService;
    }

    [RelayCommand]
    async Task CheckUpdatesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var update = await _updateService.CheckForUpdatesAsync(Settings.IncludePrereleases);
            if (update != null)
            {
                System.Windows.MessageBox.Show($"Update available: v{update.Version}\n\n{update.ReleaseNotes}", "Update Available", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("You are running the latest version.", "No Updates", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }, "Checking for updates...");
    }

    [RelayCommand]
    void ResetSettings()
    {
        var result = System.Windows.MessageBox.Show("Reset all settings to defaults? This cannot be undone.", "Confirm Reset", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _settings.ResetToDefaults();
            OnPropertyChanged(nameof(Settings));
        }
    }

    [RelayCommand]
    void OpenDataFolder()
    {
        var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaNet");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
}
