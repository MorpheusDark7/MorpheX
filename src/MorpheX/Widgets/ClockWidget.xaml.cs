using MorpheX.Services;
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
        string format = _settings.ClockIs24Hour
            ? (_settings.ClockShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (_settings.ClockShowSeconds ? "h:mm:ss tt" : "h:mm tt");
        TimeText.Text = now.ToString(format);
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
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.ClockShowBackground, 16, new Thickness(14, 8, 14, 8), _settings.WidgetTheme == 1);

        // Alignment
        var align = _settings.ClockTextAlignment switch
        {
            1 => System.Windows.HorizontalAlignment.Center,
            2 => System.Windows.HorizontalAlignment.Right,
            _ => System.Windows.HorizontalAlignment.Left
        };
        TimeText.HorizontalAlignment = align;
        DateText.HorizontalAlignment = align;

        // Glow
        if (_settings.ClockGlow)
        {
            var glowColor = _settings.ClockColorMode switch
            {
                2 => System.Windows.Media.Color.FromRgb(56, 189, 248),
                3 => System.Windows.Media.Color.FromRgb(74, 222, 128),
                4 => System.Windows.Media.Color.FromRgb(168, 85, 247),
                5 => System.Windows.Media.Color.FromRgb(251, 146, 60),
                6 => System.Windows.Media.Color.FromRgb(251, 191, 36),
                7 => System.Windows.Media.Color.FromRgb(196, 181, 253),
                8 => System.Windows.Media.Color.FromRgb(251, 113, 133),
                9 => System.Windows.Media.Color.FromRgb(147, 197, 253),
                10 => System.Windows.Media.Color.FromRgb(244, 63, 94),
                _ => System.Windows.Media.Color.FromRgb(255, 255, 255)
            };
            TimeText.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 0, Color = glowColor, Opacity = 0.75 };
        }
        else
        {
            TimeText.Effect = null;
        }

        // Opacity
        Opacity = Math.Clamp(_settings.ClockOpacity > 0 ? _settings.ClockOpacity : 1.0, 0.2, 1.0);

        // Self-heal transparent surface against black box bug
        WidgetWindowHelper.RefreshWidgetSurface(this);

        // Context menu headers
        ToggleFormatMenu.Header  = _settings.ClockIs24Hour ? "Format: 24-Hour (click for 12h)" : "Format: 12-Hour (click for 24h)";
        ToggleSecondsMenu.Header = _settings.ClockShowSeconds ? "Hide Seconds" : "Show Seconds";
        ToggleDateMenu.Header    = _settings.ClockShowDate ? "Hide Date Line" : "Show Date Line";
        ToggleCardMenu.Header    = _settings.ClockShowBackground ? "Hide Background Card" : "Show Background Card";
        ToggleGlowMenu.Header    = _settings.ClockGlow ? "Hide Neon Glow" : "Show Neon Glow";
    }

    public void ApplyTheme()
    {
        ApplyAppearance();
        Tick();
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
            7 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(167, 139, 250)),       // Soft Lavender (#A78BFA)
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 167, 139, 250))
            ),
            8 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133)),       // Rose Pink (#FB7185)
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 251, 113, 133))
            ),
            9 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(137, 180, 250)),       // Frost Sky (#89B4FA)
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 137, 180, 250))
            ),
            10 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 63, 94)),         // Crimson Red (#F43F5E)
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 244, 63, 94))
            ),
            _ => _settings.WidgetTheme == 1
                ? (
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 34, 30, 24)),  // Crisp dark charcoal for light mode
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 68, 56, 40))
                )
                : (
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
        Save();
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

    public void Reposition()
    {
        Left = _settings.ClockX;
        Top = _settings.ClockY;
        ApplyAppearance();
        Tick();
        UpdateLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    private void ToggleSeconds_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockShowSeconds = !_settings.ClockShowSeconds;
        ApplyAppearance();
        Tick();
        Save();
    }

    private void ToggleGlow_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClockGlow = !_settings.ClockGlow;
        ApplyAppearance();
        Save();
    }

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int a))
        {
            _settings.ClockTextAlignment = a;
            ApplyAppearance();
            Save();
        }
    }

    private void Opacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op))
        {
            _settings.ClockOpacity = op;
            ApplyAppearance();
            Save();
        }
    }

    private void ReloadWidget_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app?.WidgetService.ReloadWidget(typeof(ClockWidget));
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
