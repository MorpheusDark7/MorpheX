using System.Windows;
using MorpheX.Core.Configuration;
using MorpheX.Widgets;
using Application = System.Windows.Application;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Manages the lifecycle of all desktop widget windows.
/// Widgets are transparent, borderless WPF windows that sit on the desktop.
/// </summary>
public sealed class WidgetService : IDisposable
{
    private readonly WidgetSettings _settings;

    private ClockWidget? _clock;
    private SystemMonitorWidget? _sysmon;
    private MediaWidget? _media;

    private bool _disposed;

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

    public bool IsClockOpen      => _clock   != null && _clock.IsLoaded;
    public bool IsSysMonOpen     => _sysmon  != null && _sysmon.IsLoaded;
    public bool IsMediaOpen      => _media   != null && _media.IsLoaded;

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

    private static void SetWidget<T>(ref T? field, bool enabled, Func<T> factory)
        where T : Window
    {
        if (enabled)
        {
            if (field == null || !field.IsLoaded)
            {
                try
                {
                    field = factory();
                    field.Show();
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
                try
                {
                    field.Close();
                }
                catch { }
                field = null;
                Log.Debug("Widget closed: {Type}", typeof(T).Name);
            }
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
