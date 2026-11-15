using System.Text.Json;
using System.Text.Json.Serialization;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _profileTransitionDepth;

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public SettingsService(string? settingsDirectory = null)
    {
        var dir = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MorpheX");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_settingsPath, ct);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Settings = loaded;
                        Log.Information("Settings loaded from {Path}", _settingsPath);
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    BackupInvalidSettingsFile();
                    Log.Warning(ex, "Failed to parse settings file; preserved a backup and using defaults");
                }
                catch (IOException ex)
                {
                    Log.Warning(ex, "Failed to read settings file, using defaults");
                }
            }

            Settings = new AppSettings();
            await SaveInternalAsync(ct);
            Log.Information("Created default settings at {Path}", _settingsPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _profileTransitionDepth) > 0) return;

        await _lock.WaitAsync(ct);
        try
        {
            // Profile switching mutates live settings over several async steps. Defer saves
            // requested by widget closing handlers until the switch commits its final state.
            if (Volatile.Read(ref _profileTransitionDepth) > 0) return;

            await SaveInternalAsync(ct);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public IDisposable DeferSavesForProfileTransition()
    {
        Interlocked.Increment(ref _profileTransitionDepth);
        return new ProfileTransitionSaveScope(this);
    }

    private void EndProfileTransitionSaveScope() => Interlocked.Decrement(ref _profileTransitionDepth);

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            Settings = new AppSettings();
            await SaveInternalAsync(ct);
            Log.Information("Settings reset to defaults");
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(CancellationToken ct)
    {
        // Keep the active desktop profile's complete live scene in sync with settings.
        if (!string.IsNullOrEmpty(Settings.ActiveProfileId) && Settings.Profiles != null)
        {
            var active = Settings.Profiles.FirstOrDefault(p => p.Id == Settings.ActiveProfileId);
            if (active != null)
            {
                active.Widgets = DesktopProfile.CloneWidgetSettings(Settings.Widgets);
                active.WallpaperSettings = DesktopProfile.CaptureWallpaperSettings(Settings);
                active.SchemaVersion = DesktopProfile.CurrentSchemaVersion;
                DesktopProfile.SyncLegacyWallpaperFields(active);
                active.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        var tempPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    private void BackupInvalidSettingsFile()
    {
        try
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(_settingsPath)!,
                $"settings.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            File.Copy(_settingsPath, backupPath, overwrite: false);
            Log.Information("Backed up unreadable settings to {Path}", backupPath);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not back up unreadable settings file");
        }
    }

    private sealed class ProfileTransitionSaveScope : IDisposable
    {
        private SettingsService? _owner;

        public ProfileTransitionSaveScope(SettingsService owner) => _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.EndProfileTransitionSaveScope();
        }
    }
}
