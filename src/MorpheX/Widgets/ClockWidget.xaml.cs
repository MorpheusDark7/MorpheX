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

    public ClockWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.ClockX;
        Top  = settings.ClockY;

        ApplyFontSize();
        UpdateLockMenuHeader();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();

        Closing += OnClosing;
    }

    public void RefreshFormat() => Tick();

    private void Tick()
    {
        var now = DateTime.Now;
        // No seconds — clean minimal time display
        TimeText.Text = _settings.ClockIs24Hour
            ? now.ToString("HH:mm")
            : now.ToString("h:mm tt");
        DateText.Text = now.ToString("dddd, MMMM d");
    }

    private void ApplyFontSize()
    {
        double size = _settings.ClockFontSize switch
        {
            0 => 48,
            2 => 96,
            _ => 72
        };
        TimeText.FontSize = size;
        TimeText.LineHeight = size - 2;
        DateText.FontSize = size switch { >= 96 => 16, <= 48 => 11, _ => 14 };
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;

        if (e.ClickCount == 2)
        {
            _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
            Tick();
            Save();
            return;
        }

        DragMove();
    }

    private void ToggleFormat_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
        Tick();
        Save();
    }

    private void SizeSmall_Click(object sender, RoutedEventArgs e)  { _settings.ClockFontSize = 0; ApplyFontSize(); Save(); }
    private void SizeMedium_Click(object sender, RoutedEventArgs e) { _settings.ClockFontSize = 1; ApplyFontSize(); Save(); }
    private void SizeLarge_Click(object sender, RoutedEventArgs e)  { _settings.ClockFontSize = 2; ApplyFontSize(); Save(); }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        Save();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked ? "🔓  Unlock Position" : "🔒  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _settings.ClockX = Left;
        _settings.ClockY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.ClockX = Left;
        _settings.ClockY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
