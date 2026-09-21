using System.Runtime.InteropServices;
using MorpheX.Core.Configuration;
using Serilog;

namespace MorpheX.Core.Services;

/// <summary>
/// Watches system idle time and dims/restores wallpaper brightness automatically.
/// Uses GetLastInputInfo (no hooks, no global event listeners — just a lightweight poll).
/// </summary>
public sealed class AmbientDimService : IDisposable
{
    private readonly IWallpaperService _wallpaperService;
    private readonly ISettingsService  _settingsService;

    private Timer? _pollTimer;
    private bool   _isDimmed;
    private bool   _disposed;

    // Tracks the brightness override sent to effects service (-1 = not overriding)
    private float  _currentOverride = -1f;

    public AmbientDimService(IWallpaperService wallpaperService, ISettingsService settingsService)
    {
        _wallpaperService = wallpaperService;
        _settingsService  = settingsService;
    }

    public void Start()
    {
        _pollTimer?.Dispose();
        // Poll every 30 seconds — lightweight, no hooks
        _pollTimer = new Timer(Poll, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        Log.Debug("AmbientDimService started");
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        // Restore brightness immediately when stopped
        if (_isDimmed)
            RestoreBrightness();
    }

    private void Poll(object? _)
    {
        try
        {
            var settings = _settingsService.Settings.AmbientDim;
            if (!settings.Enabled)
            {
                if (_isDimmed) RestoreBrightness();
                return;
            }

            uint idleMs = GetIdleTimeMs();
            uint thresholdMs = (uint)(settings.IdleMinutes * 60 * 1000);

            if (idleMs >= thresholdMs && !_isDimmed)
            {
                ApplyDim(settings.DimLevel);
            }
            else if (idleMs < thresholdMs && _isDimmed)
            {
                RestoreBrightness();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "AmbientDimService poll error (non-fatal)");
        }
    }

    private void ApplyDim(float dimLevel)
    {
        _isDimmed = true;
        _currentOverride = dimLevel;
        _wallpaperService.ApplyEffectsToAll(brightnessOverride: dimLevel);
        Log.Information("AmbientDimService: dimmed wallpapers to {Level:P0}", dimLevel);
    }

    private void RestoreBrightness()
    {
        _isDimmed = false;
        _currentOverride = -1f;
        _wallpaperService.ApplyEffectsToAll();
        Log.Information("AmbientDimService: restored wallpaper brightness");
    }

    /// <summary>Called by WallpaperService to check if ambient dim is active.</summary>
    public bool IsDimmed => _isDimmed;
    public float CurrentOverride => _currentOverride;

    // ─── Win32 idle time ───────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private static uint GetIdleTimeMs()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return (uint)Environment.TickCount - info.dwTime;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
