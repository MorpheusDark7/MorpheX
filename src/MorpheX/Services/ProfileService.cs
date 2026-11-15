using System.IO;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX.Services;

public sealed class ProfileService
{
    private readonly SettingsService _settingsService;
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private AppSettings Settings => _settingsService.Settings;

    public event EventHandler? ProfilesChanged;

    public IReadOnlyList<DesktopProfile> Profiles => Settings.Profiles;
    public string? ActiveProfileId => Settings.ActiveProfileId;

    public ProfileService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        EnsureDefaultProfileExists();
        SanitizeProfiles();
        MigrateLegacyProfiles();
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
                IncludeWallpaper = true,
                SchemaVersion = DesktopProfile.CurrentSchemaVersion
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
            profile.MonitorWallpapers ??= new Dictionary<string, string>();
            profile.Widgets ??= new WidgetSettings();

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

            if (profile.WallpaperSettings != null)
            {
                profile.WallpaperSettings.Display ??= new DisplaySettings();
                profile.WallpaperSettings.Playback ??= new PlaybackSettings();
                profile.WallpaperSettings.Audio ??= new AudioSettings();
                profile.WallpaperSettings.Performance ??= new PerformanceSettings();
                profile.WallpaperSettings.Effects ??= new WallpaperEffectsSettings();
                profile.WallpaperSettings.AmbientDim ??= new AmbientDimSettings();
                profile.WallpaperSettings.Playlist ??= new PlaylistSettings();
                profile.WallpaperSettings.MonitorAssignments ??= new Dictionary<string, MonitorAssignment>();
            }
        }

        if (modified)
        {
            _ = _settingsService.SaveAsync();
        }
    }

    private void PopulateProfileWallpapers(DesktopProfile profile)
    {
        profile.WallpaperSettings = DesktopProfile.CaptureWallpaperSettings(Settings);
        profile.SchemaVersion = DesktopProfile.CurrentSchemaVersion;
        DesktopProfile.SyncLegacyWallpaperFields(profile);
    }

    private void CaptureProfileFromCurrentSetup(DesktopProfile profile)
    {
        profile.Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets);
        PopulateProfileWallpapers(profile);
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizePlacementMode(string? mode, Dictionary<string, string>? savedWallpapers = null)
    {
        if (savedWallpapers?.Count == 1 && savedWallpapers.ContainsKey(WallpaperService.SpanDeviceId))
            return "Span";

        if (string.Equals(mode, "Span", StringComparison.OrdinalIgnoreCase)) return "Span";
        if (string.Equals(mode, "Duplicate", StringComparison.OrdinalIgnoreCase)) return "Duplicate";
        return "PerDisplay";
    }

    private void MigrateLegacyProfiles()
    {
        if (Settings.Profiles == null) return;

        bool changed = false;
        var currentWallpaperSettings = DesktopProfile.CaptureWallpaperSettings(Settings);

        foreach (var profile in Settings.Profiles)
        {
            if (profile.SchemaVersion >= DesktopProfile.CurrentSchemaVersion && profile.WallpaperSettings != null)
                continue;

            var migrated = DesktopProfile.CloneWallpaperSettings(currentWallpaperSettings);
            if (profile.Id != Settings.ActiveProfileId)
            {
                migrated.Display.PlacementMode = NormalizePlacementMode(profile.PlacementMode, profile.MonitorWallpapers);
                foreach (var assignment in migrated.MonitorAssignments.Values)
                    assignment.WallpaperId = null;

                foreach (var entry in profile.MonitorWallpapers)
                {
                    if (!migrated.MonitorAssignments.TryGetValue(entry.Key, out var assignment))
                    {
                        assignment = new MonitorAssignment();
                        migrated.MonitorAssignments[entry.Key] = assignment;
                    }
                    assignment.WallpaperId = entry.Value;
                }
            }

            profile.WallpaperSettings = migrated;
            profile.SchemaVersion = DesktopProfile.CurrentSchemaVersion;
            DesktopProfile.SyncLegacyWallpaperFields(profile);
            changed = true;
        }

        if (changed)
        {
            Log.Information("Migrated desktop profiles to schema {SchemaVersion}; existing widget and wallpaper choices were retained",
                DesktopProfile.CurrentSchemaVersion);
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
            IncludeWallpaper = includeWallpaper,
            SchemaVersion = DesktopProfile.CurrentSchemaVersion,
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

        CaptureProfileFromCurrentSetup(profile);

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
            // The caller applies the next profile after deletion. Leave no active ID during
            // this short gap so SaveInternalAsync cannot copy the outgoing live scene into it.
            Settings.ActiveProfileId = null;
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
        Action<string>? onNotice = null,
        PlaybackService? playbackService = null,
        PlaylistService? playlistService = null,
        AmbientDimService? ambientDimService = null)
    {
        await _applyLock.WaitAsync();
        try
        {
            var profile = Settings.Profiles?.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return false;

            Log.Information("Applying desktop profile: {Name} ({Id})", profile.Name, profile.Id);

            using (_settingsService.DeferSavesForProfileTransition())
            {
                // Snapshot the outgoing live scene before target settings enter the shared
                // Settings object used by all widget windows.
                if (!string.IsNullOrEmpty(Settings.ActiveProfileId) && Settings.Profiles != null)
                {
                    var currentActive = Settings.Profiles.FirstOrDefault(p => p.Id == Settings.ActiveProfileId);
                    if (currentActive != null)
                        CaptureProfileFromCurrentSetup(currentActive);
                }

                var targetWidgets = DesktopProfile.CloneWidgetSettings(profile.Widgets);
                Settings.Widgets.CopyFrom(targetWidgets);

                // Closing an outgoing window fires its Closing handler, which writes its final
                // bounds into the shared settings object. Restore the incoming snapshot after
                // lifecycle reconciliation so those old bounds cannot leak into this profile.
                widgetService.ApplySettings();
                Settings.Widgets.CopyFrom(targetWidgets);
                widgetService.RepositionAll();
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

                if (profile.IncludeWallpaper)
                {
                    var targetWallpaperSettings = DesktopProfile.CloneWallpaperSettings(profile.WallpaperSettings);
                    RestoreWallpaperSettings(targetWallpaperSettings);
                    profile.WallpaperSettings = DesktopProfile.CloneWallpaperSettings(targetWallpaperSettings);
                    profile.SchemaVersion = DesktopProfile.CurrentSchemaVersion;
                    DesktopProfile.SyncLegacyWallpaperFields(profile);

                    bool anyMissing = await ApplyProfileWallpapersAsync(
                        profile, targetWallpaperSettings, wallpaperService, libraryService);

                    wallpaperService.ApplyEffectsToAll();
                    wallpaperService.SetPlaybackRate(Settings.Playback.PlaybackRate);
                    wallpaperService.SetVolume(Settings.Audio.Enabled ? Settings.Audio.Volume / 100f : -1f);
                    playbackService?.PollNow();
                    playlistService?.UpdateSettings(triggerChangeOnStartup: false);
                    ambientDimService?.RefreshAfterProfileChange();

                    if (anyMissing)
                        onNotice?.Invoke($"Switched to '{profile.Name}'. Some saved wallpaper files were deleted or moved, so current wallpapers were preserved.");
                }

                widgetService.RefreshClockSurface();
                Settings.ActiveProfileId = profile.Id;
                profile.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _settingsService.SaveAsync();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            _applyLock.Release();
        }
    }

    private void RestoreWallpaperSettings(ProfileWallpaperSettings target)
    {
        var currentAssignments = Settings.MonitorAssignments;
        Settings.Display = target.Display;
        Settings.Playback = target.Playback;
        Settings.Audio = target.Audio;
        Settings.Performance = target.Performance;
        Settings.Effects = target.Effects;
        Settings.AmbientDim = target.AmbientDim;
        Settings.Playlist = target.Playlist;

        foreach (var assignment in currentAssignments)
        {
            if (!target.MonitorAssignments.ContainsKey(assignment.Key))
                target.MonitorAssignments[assignment.Key] = assignment.Value;
        }
        Settings.MonitorAssignments = target.MonitorAssignments;
    }

    private async Task<bool> ApplyProfileWallpapersAsync(
        DesktopProfile profile,
        ProfileWallpaperSettings wallpaperSettings,
        WallpaperService wallpaperService,
        LibraryService libraryService)
    {
        var savedWallpapers = new Dictionary<string, string>(profile.MonitorWallpapers);
        foreach (var entry in wallpaperSettings.MonitorAssignments)
        {
            if (!string.IsNullOrWhiteSpace(entry.Value.WallpaperId))
                savedWallpapers[entry.Key] = entry.Value.WallpaperId;
        }

        if (savedWallpapers.Count == 0) return false;

        string placement = NormalizePlacementMode(wallpaperSettings.Display.PlacementMode, savedWallpapers);
        wallpaperSettings.Display.PlacementMode = placement;
        bool anyMissing = false;

        if (string.Equals(placement, "Span", StringComparison.OrdinalIgnoreCase))
        {
            string? wallpaperId = savedWallpapers.GetValueOrDefault(WallpaperService.SpanDeviceId)
                ?? savedWallpapers.Values.FirstOrDefault();
            var wallpaper = ResolveWallpaper(libraryService, wallpaperId);
            if (IsWallpaperAvailable(wallpaper))
            {
                try
                {
                    await wallpaperService.SetSpannedWallpaperAsync(wallpaper!, skipSave: true);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to apply spanned wallpaper for profile {Profile}", profile.Name);
                    anyMissing = true;
                }
            }
            else
            {
                anyMissing = true;
            }
        }
        else
        {
            if (wallpaperService.GetActiveWallpaper(WallpaperService.SpanDeviceId) != null)
                await wallpaperService.RemoveWallpaperAsync(WallpaperService.SpanDeviceId);

            IEnumerable<KeyValuePair<string, string>> entries;
            if (string.Equals(placement, "Duplicate", StringComparison.OrdinalIgnoreCase))
            {
                string? sharedId = savedWallpapers.FirstOrDefault(entry => entry.Key != WallpaperService.SpanDeviceId).Value;
                entries = savedWallpapers.Keys
                    .Where(key => key != WallpaperService.SpanDeviceId)
                    .Select(key => new KeyValuePair<string, string>(key, sharedId ?? string.Empty));
            }
            else
            {
                entries = savedWallpapers.Where(entry => entry.Key != WallpaperService.SpanDeviceId);
            }

            foreach (var entry in entries)
            {
                var wallpaper = ResolveWallpaper(libraryService, entry.Value);
                if (!IsWallpaperAvailable(wallpaper))
                {
                    anyMissing = true;
                    Log.Information("Profile wallpaper '{Id}' is unavailable; keeping the current wallpaper on {Monitor}.", entry.Value, entry.Key);
                    continue;
                }

                try
                {
                    await wallpaperService.SetWallpaperAsync(entry.Key, wallpaper!, skipSave: true);
                }
                catch (Exception ex)
                {
                    anyMissing = true;
                    Log.Warning(ex, "Failed to apply wallpaper for monitor {Monitor}", entry.Key);
                }
            }
        }

        Settings.Display.PlacementMode = placement;
        return anyMissing;
    }

    private static MorpheX.Core.Models.WallpaperInfo? ResolveWallpaper(LibraryService libraryService, string? wallpaperIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(wallpaperIdOrPath)) return null;
        return libraryService.GetById(wallpaperIdOrPath)
            ?? libraryService.Wallpapers.FirstOrDefault(w =>
                string.Equals(w.EffectivePath, wallpaperIdOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(w.SourcePath, wallpaperIdOrPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWallpaperAvailable(MorpheX.Core.Models.WallpaperInfo? wallpaper)
    {
        if (wallpaper == null) return false;
        return wallpaper.Type == WallpaperType.Scene
            ? Directory.Exists(wallpaper.EffectivePath) || File.Exists(wallpaper.EffectivePath)
            : File.Exists(wallpaper.EffectivePath);
    }
}
