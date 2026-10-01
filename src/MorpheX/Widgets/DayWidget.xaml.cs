using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class DayWidget : Window
{
    private readonly WidgetSettings _settings;

    public DayWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.DayX;
        Top  = settings.DayY;

        ApplyFontSize();
        ApplyColor();
        UpdateLockMenuHeader();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
        Tick();

        Closing += OnClosing;
    }

    private void Tick() => DayText.Text = DateTime.Now.ToString("dddd").ToUpperInvariant();

    private void ApplyFontSize()
    {
        DayText.FontSize = _settings.DayFontSize switch
        {
            0 => 48,
            2 => 96,
            _ => 72
        };
    }

    private void ApplyColor()
    {
        DayText.Foreground = _settings.DayColorMode switch
        {
            1 => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(140, 255, 255, 255)),
            2 => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(56, 189, 248)),
            _ => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(204, 255, 255, 255))
        };
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void SizeSmall_Click(object sender, RoutedEventArgs e)  { _settings.DayFontSize = 0; ApplyFontSize(); Save(); }
    private void SizeMedium_Click(object sender, RoutedEventArgs e) { _settings.DayFontSize = 1; ApplyFontSize(); Save(); }
    private void SizeLarge_Click(object sender, RoutedEventArgs e)  { _settings.DayFontSize = 2; ApplyFontSize(); Save(); }

    private void ColorWhite_Click(object sender, RoutedEventArgs e) { _settings.DayColorMode = 0; ApplyColor(); Save(); }
    private void ColorDim_Click(object sender, RoutedEventArgs e)   { _settings.DayColorMode = 1; ApplyColor(); Save(); }
    private void ColorCyan_Click(object sender, RoutedEventArgs e)  { _settings.DayColorMode = 2; ApplyColor(); Save(); }

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
        _settings.DayEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.DayX = Left;
        _settings.DayY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.DayX = Left;
        _settings.DayY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
