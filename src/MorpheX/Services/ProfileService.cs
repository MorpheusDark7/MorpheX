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
                TaskbarStyle = Settings.Taskbar.Style,
                IncludeWallpaper = true
            };

            foreach (var kvp in Settings.MonitorAssignments)
            {
                if (!string.IsNullOrEmpty(kvp.Value.WallpaperId))
                {
                    defaultProfile.MonitorWallpapers[kvp.Key] = kvp.Value.WallpaperId;
                }
            }

            Settings.Profiles.Add(defaultProfile);
            Settings.ActiveProfileId = defaultProfile.Id;
            _ = _settingsService.SaveAsync();
        }
    }

    public async Task<DesktopProfile> CreateProfileAsync(string name, bool includeWallpaper = true)
    {
        Settings.Profiles ??= new List<DesktopProfile>();

        var profile = new DesktopProfile
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Profile {Settings.Profiles.Count + 1}" : name.Trim(),
            Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets),
            TaskbarStyle = Settings.Taskbar.Style,
            IncludeWallpaper = includeWallpaper,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (includeWallpaper)
        {
            foreach (var kvp in Settings.MonitorAssignments)
            {
                if (!string.IsNullOrEmpty(kvp.Value.WallpaperId))
                {
                    profile.MonitorWallpapers[kvp.Key] = kvp.Value.WallpaperId;
                }
            }
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
        profile.TaskbarStyle = Settings.Taskbar.Style;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        if (profile.IncludeWallpaper)
        {
            profile.MonitorWallpapers.Clear();
            foreach (var kvp in Settings.MonitorAssignments)
            {
                if (!string.IsNullOrEmpty(kvp.Value.WallpaperId))
                {
                    profile.MonitorWallpapers[kvp.Key] = kvp.Value.WallpaperId;
                }
            }
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

        // 1. Copy widget settings snapshot into active settings
        Settings.Widgets = DesktopProfile.CloneWidgetSettings(profile.Widgets);

        // 2. Re-apply widget window lifecycle and properties
        widgetService.ApplySettings();
        widgetService.RefreshLockState();
        widgetService.RefreshTheme();
        widgetService.RefreshMedia();
        widgetService.RefreshClock();
        widgetService.RefreshDay();
        widgetService.RefreshSysMon();
        widgetService.RefreshWeather();

        // 3. Update taskbar visual style
        Settings.Taskbar.Style = profile.TaskbarStyle;

        // 4. Optionally restore saved wallpapers (with deleted-wallpaper safety!)
        if (profile.IncludeWallpaper && profile.MonitorWallpapers != null && profile.MonitorWallpapers.Count > 0)
        {
            bool anyMissing = false;
            foreach (var kvp in profile.MonitorWallpapers)
            {
                var monitorId = kvp.Key;
                var wallpaperIdOrPath = kvp.Value;

                // Look up by ID first, then by path
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
                    // Wallpaper was moved or deleted — protect user and keep current wallpaper playing
                    anyMissing = true;
                    Log.Information("Profile wallpaper '{Id}' no longer exists on disk; keeping current wallpaper.", wallpaperIdOrPath);
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
