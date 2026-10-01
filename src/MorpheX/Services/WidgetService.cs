using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MorpheX.Core.Configuration;
using MorpheX.Widgets;
using Application = System.Windows.Application;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Manages the lifecycle of all desktop widget windows.
/// Widgets are transparent, borderless WPF windows that live in the desktop Z-order layer —
/// below normal application windows, above the wallpaper.
/// This mirrors the Rainmeter approach: pin to HWND_BOTTOM so they never cover other apps.
/// </summary>
public sealed class WidgetService : IDisposable
{
    private readonly WidgetSettings _settings;

    private ClockWidget? _clock;
    private SystemMonitorWidget? _sysmon;
    private MediaWidget? _media;

    private bool _disposed;

    // Win32 Z-order constants
    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    public WidgetService(WidgetSettings settings)
    {
        _settings = settings;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void ApplySettings()
    {
        if (Application.Current.Dispatcher.CheckAccess())
            ApplySettingsCore();
        else
            Application.Current.Dispatcher.Invoke(ApplySettingsCore);
    }

    public bool IsClockOpen  => _clock  != null && _clock.IsVisible;
    public bool IsSysMonOpen => _sysmon != null && _sysmon.IsVisible;
    public bool IsMediaOpen  => _media  != null && _media.IsVisible;

    public void RefreshClock()
    {
        _clock?.Dispatcher.InvokeAsync(() => _clock.RefreshFormat());
    }

    // ── Core (must run on UI thread) ─────────────────────────────────────────

    private void ApplySettingsCore()
    {
        SetWidget(ref _clock,
            _settings.ClockEnabled,
            () => new ClockWidget(_settings));

        SetWidget(ref _sysmon,
            _settings.SystemMonitorEnabled,
            () => new SystemMonitorWidget(_settings));

        SetWidget(ref _media,
            _settings.MediaEnabled,
            () => new MediaWidget(_settings));
    }

    private void SetWidget<T>(ref T? field, bool enabled, Func<T> factory)
        where T : Window
    {
        if (enabled)
        {
            if (field == null || !field.IsVisible)
            {
                try
                {
                    field = factory();
                    field.Show();
                    SendToDesktop(field);
                    Log.Debug("Widget opened: {Type}", typeof(T).Name);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to open widget {Type}", typeof(T).Name);
                }
            }
        }
        else
        {
            if (field != null)
            {
                try { field.Close(); } catch { }
                field = null;
                Log.Debug("Widget closed: {Type}", typeof(T).Name);
            }
        }
    }

    /// <summary>
    /// Pins the widget to the bottom of the Z-order so it sits below all normal windows.
    /// Uses SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE so only Z-order changes.
    /// </summary>
    private static void SendToDesktop(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SendToDesktop failed for widget");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Application.Current?.Dispatcher.Invoke(() =>
        {
            CloseWidget(ref _clock);
            CloseWidget(ref _sysmon);
            CloseWidget(ref _media);
        });
    }

    private static void CloseWidget<T>(ref T? field) where T : Window
    {
        if (field == null) return;
        try { field.Close(); } catch { }
        field = null;
    }
}


