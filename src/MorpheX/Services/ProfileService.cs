using System.IO;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX.Services;

public sealed class ProfileService
{
    private readonly SettingsService _settingsService;
    private AppSettings Settings => _settingsService.Settings;

    public event EventHandler? ProfilesChanged;

    public IReadOnlyList<DesktopProfile> Profiles => Settings.Profiles;
    public string? ActiveProfileId => Settings.ActiveProfileId;

    public ProfileService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        EnsureDefaultProfileExists();
        SanitizeProfiles();
    }

    private void EnsureDefaultProfileExists()
    {
        if (Settings.Profiles == null || Settings.Profiles.Count == 0)
        {
            Settings.Profiles ??= new List<DesktopProfile>();
            var defaultProfile = new DesktopProfile
            {
                Name = "Default Setup",
                Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets),
                IncludeWallpaper = true
            };

            PopulateProfileWallpapers(defaultProfile);

            Settings.Profiles.Add(defaultProfile);
            Settings.ActiveProfileId = defaultProfile.Id;
            _ = _settingsService.SaveAsync();
        }
    }

    private void SanitizeProfiles()
    {
        if (Settings.Profiles == null) return;
        bool modified = false;

        foreach (var profile in Settings.Profiles)
        {
            bool hasSpan = profile.MonitorWallpapers.ContainsKey(MorpheX.Core.Services.WallpaperService.SpanDeviceId);
            int nonSpanCount = profile.MonitorWallpapers.Keys.Count(k => k != MorpheX.Core.Services.WallpaperService.SpanDeviceId);

            // Clean up old profiles where SPAN was erroneously included alongside physical monitors
            if (hasSpan && nonSpanCount > 0)
            {
                profile.MonitorWallpapers.Remove(MorpheX.Core.Services.WallpaperService.SpanDeviceId);
                profile.PlacementMode = "PerDisplay";
                modified = true;
            }
            else if (hasSpan && nonSpanCount == 0)
            {
                profile.PlacementMode = "Span";
            }
            else if (string.IsNullOrEmpty(profile.PlacementMode))
            {
                profile.PlacementMode = "PerDisplay";
            }
        }

        if (modified)
        {
            _ = _settingsService.SaveAsync();
        }
    }

    private void PopulateProfileWallpapers(DesktopProfile profile)
    {
        profile.MonitorWallpapers.Clear();
        string placement = Settings.Display.PlacementMode;
        profile.PlacementMode = string.Equals(placement, "Span", StringComparison.OrdinalIgnoreCase) ? "Span" : "PerDisplay";

        if (profile.PlacementMode == "Span")
        {
            if (Settings.MonitorAssignments.TryGetValue(MorpheX.Core.Services.WallpaperService.SpanDeviceId, out var spanAssign)
                && !string.IsNullOrEmpty(spanAssign.WallpaperId))
            {
                profile.MonitorWallpapers[MorpheX.Core.Services.WallpaperService.SpanDeviceId] = spanAssign.WallpaperId;
            }
        }
        else
        {
            foreach (var kvp in Settings.MonitorAssignments)
            {
                if (kvp.Key != MorpheX.Core.Services.WallpaperService.SpanDeviceId && !string.IsNullOrEmpty(kvp.Value.WallpaperId))
                {
                    profile.MonitorWallpapers[kvp.Key] = kvp.Value.WallpaperId;
                }
            }
        }
    }

    public async Task<DesktopProfile> CreateProfileAsync(string name, bool includeWallpaper = true)
    {
        Settings.Profiles ??= new List<DesktopProfile>();

        var profile = new DesktopProfile
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Profile {Settings.Profiles.Count + 1}" : name.Trim(),
            Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets),
            IncludeWallpaper = includeWallpaper,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (includeWallpaper)
        {
            PopulateProfileWallpapers(profile);
        }

        Settings.Profiles.Add(profile);
        Settings.ActiveProfileId = profile.Id;

        await _settingsService.SaveAsync();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("Created desktop profile: {Name} ({Id})", profile.Name, profile.Id);
        return profile;
    }

    public async Task<bool> UpdateProfileWithCurrentSetupAsync(string profileId)
    {
        var profile = Settings.Profiles?.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        profile.Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets);
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        if (profile.IncludeWallpaper)
        {
            PopulateProfileWallpapers(profile);
        }

        Settings.ActiveProfileId = profile.Id;
        await _settingsService.SaveAsync();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("Updated desktop profile: {Name} ({Id})", profile.Name, profile.Id);
        return true;
    }

    public async Task<bool> RenameProfileAsync(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;

        var profile = Settings.Profiles?.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        profile.Name = newName.Trim();
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _settingsService.SaveAsync();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        if (Settings.Profiles == null || Settings.Profiles.Count <= 1)
        {
            // Always keep at least 1 profile
            return false;
        }

        var profile = Settings.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        Settings.Profiles.Remove(profile);
        if (Settings.ActiveProfileId == profileId)
        {
            Settings.ActiveProfileId = Settings.Profiles.FirstOrDefault()?.Id;
        }

        await _settingsService.SaveAsync();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("Deleted desktop profile: {Name} ({Id})", profile.Name, profile.Id);
        return true;
    }

    public async Task<bool> ApplyProfileAsync(
        string profileId,
        WallpaperService wallpaperService,
        LibraryService libraryService,
        WidgetService widgetService,
        Action<string>? onNotice = null)
    {
        var profile = Settings.Profiles?.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return false;

        Log.Information("Applying desktop profile: {Name} ({Id})", profile.Name, profile.Id);

        // Snapshot outgoing active profile before switching away
        if (!string.IsNullOrEmpty(Settings.ActiveProfileId) && Settings.ActiveProfileId != profileId && Settings.Profiles != null)
        {
            var currentActive = Settings.Profiles.FirstOrDefault(p => p.Id == Settings.ActiveProfileId);
            if (currentActive != null)
            {
                currentActive.Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets);
                currentActive.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        // 1. Copy target profile widget snapshot into active settings in-place
        Settings.Widgets.CopyFrom(profile.Widgets);

        // 2. Re-apply widget window lifecycle and properties across all widgets
        widgetService.ApplySettings();
        widgetService.RefreshLockState();
        widgetService.RefreshTheme();
        widgetService.RefreshMedia();
        widgetService.RefreshClock();
        widgetService.RefreshDay();
        widgetService.RefreshSysMon();
        widgetService.RefreshWeather();
        widgetService.RefreshCalendar();
        widgetService.RefreshNotes();
        widgetService.RefreshQuote();

        // 3. Restore saved wallpapers cleanly without conflicting span vs per-display states
        if (profile.IncludeWallpaper && profile.MonitorWallpapers != null && profile.MonitorWallpapers.Count > 0)
        {
            bool isSpanProfile = string.Equals(profile.PlacementMode, "Span", StringComparison.OrdinalIgnoreCase)
                || (profile.MonitorWallpapers.ContainsKey(MorpheX.Core.Services.WallpaperService.SpanDeviceId) && profile.MonitorWallpapers.Count == 1);

            bool anyMissing = false;

            if (isSpanProfile)
            {
                var wpId = profile.MonitorWallpapers.Values.FirstOrDefault();
                if (!string.IsNullOrEmpty(wpId))
                {
                    var wallpaper = libraryService.GetById(wpId)
                        ?? libraryService.Wallpapers.FirstOrDefault(w =>
                            string.Equals(w.EffectivePath, wpId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(w.SourcePath, wpId, StringComparison.OrdinalIgnoreCase));

                    if (wallpaper != null && File.Exists(wallpaper.EffectivePath))
                    {
                        try
                        {
                            await wallpaperService.SetSpannedWallpaperAsync(wallpaper, skipSave: true);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to apply spanned wallpaper for profile {Profile}", profile.Name);
                        }
                    }
                    else
                    {
                        anyMissing = true;
                    }
                }
            }
            else
            {
                // PerDisplay mode: ensure Spanned wallpaper is removed so it doesn't conflict
                if (wallpaperService.GetActiveWallpaper(MorpheX.Core.Services.WallpaperService.SpanDeviceId) != null)
                {
                    await wallpaperService.RemoveWallpaperAsync(MorpheX.Core.Services.WallpaperService.SpanDeviceId);
                }

                Settings.Display.PlacementMode = "PerDisplay";

                foreach (var kvp in profile.MonitorWallpapers)
                {
                    var monitorId = kvp.Key;
                    if (monitorId == MorpheX.Core.Services.WallpaperService.SpanDeviceId) continue;

                    var wallpaperIdOrPath = kvp.Value;

                    var wallpaper = libraryService.GetById(wallpaperIdOrPath)
                        ?? libraryService.Wallpapers.FirstOrDefault(w =>
                            string.Equals(w.EffectivePath, wallpaperIdOrPath, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(w.SourcePath, wallpaperIdOrPath, StringComparison.OrdinalIgnoreCase));

                    if (wallpaper != null && File.Exists(wallpaper.EffectivePath))
                    {
                        try
                        {
                            await wallpaperService.SetWallpaperAsync(monitorId, wallpaper, skipSave: true);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to apply wallpaper for monitor {Monitor}", monitorId);
                        }
                    }
                    else
                    {
                        anyMissing = true;
                        Log.Information("Profile wallpaper '{Id}' no longer exists on disk; keeping current wallpaper.", wallpaperIdOrPath);
                    }
                }
            }

            if (anyMissing)
            {
                onNotice?.Invoke($"Switched to '{profile.Name}'. Some saved wallpaper files were deleted or moved, so current wallpapers were preserved.");
            }
        }

        Settings.ActiveProfileId = profile.Id;
        await _settingsService.SaveAsync();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }
}
