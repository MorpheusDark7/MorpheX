using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class ClockWidget : Window
{
    private readonly DispatcherTimer _timer;
    private readonly WidgetSettings _settings;
    private DispatcherTimer? _saveDebounceTimer;

    public ClockWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left   = settings.ClockX;
        Top    = settings.ClockY;
        Width  = Math.Max(160, settings.ClockWidth);
        Height = Math.Max(60,  settings.ClockHeight);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();

        Loaded   += (_, _) => UpdateLockMenuHeader();
        Closing  += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    public void RefreshFormat()
    {
        Tick();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        TimeText.Text = _settings.ClockIs24Hour
            ? now.ToString("HH:mm:ss")
            : now.ToString("h:mm:ss tt");
        DateText.Text = now.ToString("dddd, MMMM d");
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;

        if (e.ClickCount == 2)
        {
            _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
            Tick();
            var app = (App)Application.Current;
            _ = app.SettingsService.SaveAsync();
            return;
        }

        DragMove();
    }

    private void ToggleFormat_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
        Tick();
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void ResetSize_Click(object sender, RoutedEventArgs e)
    {
        Width  = 220;
        Height = 85;
        _settings.ClockWidth  = Width;
        _settings.ClockHeight = Height;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked
            ? "\ud83d\udd13  Unlock Position"
            : "\ud83d\udd12  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockEnabled = false;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
        Close();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settings.ClockWidth  = ActualWidth;
        _settings.ClockHeight = ActualHeight;
        ScheduleDebouncedSave();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _settings.ClockX      = Left;
        _settings.ClockY      = Top;
        _settings.ClockWidth  = ActualWidth;
        _settings.ClockHeight = ActualHeight;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.ClockX = Left;
        _settings.ClockY = Top;
    }

    private void ScheduleDebouncedSave()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            var app = (App)Application.Current;
            _ = app.SettingsService.SaveAsync();
        };
        _saveDebounceTimer.Start();
    }
}
