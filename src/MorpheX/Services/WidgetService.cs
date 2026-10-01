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

    private ClockWidget?         _clock;
    private SystemMonitorWidget? _sysmon;
    private MediaWidget?         _media;
    private DayWidget?           _day;
    private CalendarWidget?      _calendar;
    private NotesWidget?         _notes;
    private QuoteWidget?         _quote;

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

    public bool IsClockOpen    => _clock    != null && _clock.IsVisible;
    public bool IsSysMonOpen   => _sysmon   != null && _sysmon.IsVisible;
    public bool IsMediaOpen    => _media    != null && _media.IsVisible;
    public bool IsDayOpen      => _day      != null && _day.IsVisible;
    public bool IsCalendarOpen => _calendar != null && _calendar.IsVisible;
    public bool IsNotesOpen    => _notes    != null && _notes.IsVisible;
    public bool IsQuoteOpen    => _quote    != null && _quote.IsVisible;

    public void RefreshClock()
    {
        _clock?.Dispatcher.InvokeAsync(() => _clock.RefreshFormat());
    }

    public void RefreshMedia()
    {
        _media?.Dispatcher.InvokeAsync(() => _media.RefreshVisualizerStyle());
    }

    // ── Core (must run on UI thread) ─────────────────────────────────────────

    private void ApplySettingsCore()
    {
        SetWidget(ref _clock,    _settings.ClockEnabled,          () => new ClockWidget(_settings));
        SetWidget(ref _sysmon,   _settings.SystemMonitorEnabled,   () => new SystemMonitorWidget(_settings));
        SetWidget(ref _media,    _settings.MediaEnabled,           () => new MediaWidget(_settings));
        SetWidget(ref _day,      _settings.DayEnabled,             () => new DayWidget(_settings));
        SetWidget(ref _calendar, _settings.CalendarEnabled,        () => new CalendarWidget(_settings));
        SetWidget(ref _notes,    _settings.NotesEnabled,           () => new NotesWidget(_settings));
        SetWidget(ref _quote,    _settings.QuoteEnabled,           () => new QuoteWidget(_settings));
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
                    WidgetWindowHelper.SetupWidgetWindow(field);
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
                try { field.Close(); } catch { }
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
            CloseWidget(ref _day);
            CloseWidget(ref _calendar);
            CloseWidget(ref _notes);
            CloseWidget(ref _quote);
        });
    }

    private static void CloseWidget<T>(ref T? field) where T : Window
    {
        if (field == null) return;
        try { field.Close(); } catch { }
        field = null;
    }
}
