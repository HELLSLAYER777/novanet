using System.Text.Json;
using NovaNet.Models;

namespace NovaNet.Services;

/// <summary>Manages persistent application settings.</summary>
public sealed class SettingsService
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService() => Load();

    public void Load()
    {
        try
        {
            if (File.Exists(AppSettings.SettingsPath))
            {
                var json = File.ReadAllText(AppSettings.SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
        // Ensure defaults for new properties
        Settings.PingTargets ??= new[] { "1.1.1.1", "8.8.8.8" };
        Settings.SpeedTestServers ??= new[] { "https://speed.cloudflare.com" };
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppSettings.SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(AppSettings.SettingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
            SettingsChanged?.Invoke(Settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex}");
        }
    }

    public void UpdateTheme(ThemeMode theme)
    {
        Settings.Theme = theme;
        Save();
    }

    public void UpdatePingTargets(IEnumerable<string> targets)
    {
        Settings.PingTargets = targets.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToArray();
        Save();
    }

    public void ResetToDefaults()
    {
        Settings = new AppSettings();
        Save();
    }
}