using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;

namespace MorpheX.Widgets;

public partial class ClockWidget : Window
{
    private readonly DispatcherTimer _timer;
    private readonly WidgetSettings _settings;

    private static readonly System.Windows.Media.FontFamily BahnschriftFont = new("Bahnschrift Light, Segoe UI Light, Segoe UI");
    private static readonly System.Windows.Media.FontFamily SegoeUiFont = new("Segoe UI Light, Segoe UI");
    private static readonly System.Windows.Media.FontFamily AnuratiFont = new("/MorpheX;component/Assets/Fonts/Anurati-Regular.otf#Anurati, Bahnschrift Light");
    private static readonly System.Windows.Media.FontFamily ConsolasFont = new("Consolas, Courier New");

    public ClockWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.ClockX;
        Top  = settings.ClockY;

        ApplyAppearance();
        UpdateLockState();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();

        Closing += OnClosing;
    }

    public void RefreshFormat()
    {
        ApplyAppearance();
        Tick();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        // No seconds — clean minimal time display
        TimeText.Text = _settings.ClockIs24Hour
            ? now.ToString("HH:mm")
            : now.ToString("h:mm tt");
        DateText.Text = now.ToString("dddd, MMMM d");
    }

    public void ApplyAppearance()
    {
        double size = _settings.ClockFontSize switch
        {
            0 => 40,
            1 => 64,
            2 => 88,
            3 => 112,
            4 => 140,
            _ => 64
        };

        TimeText.FontSize = size;
        DateText.FontSize = size switch { >= 110 => 18, >= 80 => 16, <= 40 => 11, _ => 13 };

        // Color
        var (timeBrush, dateBrush) = GetColorBrushes();
        TimeText.Foreground = timeBrush;
        DateText.Foreground = dateBrush;

        // Font
        var fontFamily = _settings.ClockFontMode switch
        {
            1 => SegoeUiFont,
            2 => WidgetStyles.GetAnuratiFont(),
            3 => ConsolasFont,
            4 => WidgetStyles.GetQuicksandFont(),
            _ => BahnschriftFont
        };
        TimeText.FontFamily = fontFamily;

        // Date visibility
        DateText.Visibility = _settings.ClockShowDate ? Visibility.Visible : Visibility.Collapsed;

        // Card background (translucent smoked glass, not an opaque black box)
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.ClockShowBackground, 16, new Thickness(14, 8, 14, 8));

        // Context menu headers
        ToggleFormatMenu.Header = _settings.ClockIs24Hour ? "⏰  Format: 24-Hour (Click for 12h)" : "⏰  Format: 12-Hour (Click for 24h)";
        ToggleDateMenu.Header = _settings.ClockShowDate ? "📅  Hide Date Line" : "📅  Show Date Line";
        ToggleCardMenu.Header = _settings.ClockShowBackground ? "🔲  Hide Background Card" : "🔲  Show Background Card";
    }

    private (System.Windows.Media.Brush time, System.Windows.Media.Brush date) GetColorBrushes()
    {
        return _settings.ClockColorMode switch
        {
            1 => (
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 203, 213, 225)), // Dim Slate
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 148, 163, 184))
            ),
            2 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),        // Electric Cyan
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 56, 189, 248))
            ),
            3 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)),        // Neon Mint
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 74, 222, 128))
            ),
            4 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252)),       // Cyber Violet
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 192, 132, 252))
            ),
            5 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),        // Sunset Orange
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 251, 146, 60))
            ),
            6 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)),        // Amber Gold
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 251, 191, 36))
            ),
            _ => (
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 255, 255, 255)), // Pure White
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 255, 255, 255))
            )
        };
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;

        if (e.ClickCount == 2)
        {
            _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
            ApplyAppearance();
            Tick();
            Save();
            return;
        }

        DragMove();
    }

    private void ToggleFormat_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
        ApplyAppearance();
        Tick();
        Save();
    }

    private void ToggleDate_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockShowDate = !_settings.ClockShowDate;
        ApplyAppearance();
        Save();
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockShowBackground = !_settings.ClockShowBackground;
        ApplyAppearance();
        Save();
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.ClockFontSize = val;
            ApplyAppearance();
            Save();
        }
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.ClockColorMode = val;
            ApplyAppearance();
            Save();
        }
    }

    private void FontPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.ClockFontMode = val;
            ApplyAppearance();
            Save();
        }
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        Save();
        (Application.Current as App)?.WidgetService.RefreshLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
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
