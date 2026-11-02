using System.Net.Http;
using System.Text.Json;
using NovaNet.Models;

namespace NovaNet.Services;

/// <summary>Checks for application updates from GitHub Releases.</summary>
public sealed class UpdateService
{
    const string RepoOwner = "your-github-username"; // Replace with actual repo
    const string RepoName = "NovaNet";
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly SettingsService _settings;

    public UpdateService(SettingsService settings)
    {
        _settings = settings;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NovaNet/1.1");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool includePrereleases = false)
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases";
            if (!includePrereleases) url += "?per_page=10";

            var json = await _http.GetStringAsync(url);
            var releases = JsonSerializer.Deserialize<GitHubRelease[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (releases == null || releases.Length == 0) return null;

            var latest = includePrereleases ? releases[0] : releases.FirstOrDefault(r => !r.Prerelease);
            if (latest == null) return null;

            Version? currentVersion = Version.TryParse("1.1.0", out var v) ? v : null;
            Version? latestVersion = Version.TryParse(latest.TagName.TrimStart('v'), out var lv) ? lv : null;

            if (currentVersion != null && latestVersion != null && latestVersion > currentVersion)
            {
                var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
                return new UpdateInfo
                {
                    Version = latestVersion.ToString(),
                    ReleaseNotes = latest.Body ?? "No release notes.",
                    DownloadUrl = asset?.BrowserDownloadUrl ?? latest.HtmlUrl,
                    PublishedAt = latest.PublishedAt,
                    IsPrerelease = latest.Prerelease
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex}");
        }
        return null;
    }

    public async Task CheckAndNotifyAsync()
    {
        if (!_settings.Settings.AutoCheckUpdates) return;
        
        var update = await CheckForUpdatesAsync(_settings.Settings.IncludePrereleases);
        if (update != null)
        {
            // Notify via tray or in-app
            System.Diagnostics.Debug.WriteLine($"Update available: {update.Version}");
        }
        
        _settings.Settings.LastUpdateCheck = DateTime.UtcNow;
        _settings.Settings.LastKnownVersion = update?.Version;
        _settings.Save();
    }
}

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public bool IsPrerelease { get; set; }
}

sealed class GitHubRelease
{
    public string TagName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Prerelease { get; set; }
    public DateTime PublishedAt { get; set; }
    public string HtmlUrl { get; set; } = "";
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

sealed class GitHubAsset
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
}