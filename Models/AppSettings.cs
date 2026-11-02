using System.Text.Json.Serialization;

namespace NovaNet.Models;

/// <summary>Application theme mode.</summary>
public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

/// <summary>Persistent application settings.</summary>
public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool AutoStartMonitoring { get; set; } = true;
    public int PingIntervalSeconds { get; set; } = 1;
    public int HistoryRetentionHours { get; set; } = 24;
    public bool EnableNotifications { get; set; } = true;
    public double LatencyWarningThresholdMs { get; set; } = 100;
    public double LatencyCriticalThresholdMs { get; set; } = 250;
    public string[] SpeedTestServers { get; set; } = { "https://speed.cloudflare.com" };
    public string[] PingTargets { get; set; } = { "1.1.1.1", "8.8.8.8" };
    public bool AutoCheckUpdates { get; set; } = true;
    public bool IncludePrereleases { get; set; }
    public DateTime? LastUpdateCheck { get; set; }
    public string? LastKnownVersion { get; set; }

    [JsonIgnore]
    public static string SettingsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NovaNet", "settings.json");
}