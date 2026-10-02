using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class DayWidget : Window
{
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _timer;

    private static readonly System.Windows.Media.FontFamily AnuratiFont = new("/MorpheX;component/Assets/Fonts/Anurati-Regular.otf#Anurati, Bahnschrift Light, Segoe UI Light");
    private static readonly System.Windows.Media.FontFamily BahnschriftFont = new("Bahnschrift Light, Segoe UI Light, Segoe UI");
    private static readonly System.Windows.Media.FontFamily SegoeUiFont = new("Segoe UI Light, Segoe UI");
    private static readonly System.Windows.Media.FontFamily ConsolasFont = new("Consolas, Courier New");

    public DayWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.DayX;
        Top  = settings.DayY;

        UpdateLockState();
        UpdateDayDisplay();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => UpdateDayDisplay();
        _timer.Start();

        Closing += OnClosing;
    }

    public void RefreshDisplay() => UpdateDayDisplay();

    private void UpdateDayDisplay()
    {
        DayLettersPanel.Children.Clear();

        string day = DateTime.Now.ToString("dddd");
        day = _settings.DayCaseMode switch
        {
            1 => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(day.ToLowerInvariant()),
            2 => day.ToLowerInvariant(),
            _ => day.ToUpperInvariant()
        };

        double fontSize = _settings.DayFontSize switch
        {
            0 => 42,
            1 => 64,
            2 => 86,
            3 => 110,
            4 => 140,
            5 => 180,
            _ => 64
        };

        var brush = GetColorBrush();
        var fontFamily = GetFontFamily();

        // Anurati only contains uppercase glyphs (Mond signature style). Ensure uppercase.
        bool isAnurati = _settings.DayFontMode == 0;
        if (isAnurati)
        {
            day = day.ToUpperInvariant();
        }

        // Base letter spacing proportional to font size (wide airy Mond tracking)
        double trackingMultiplier = _settings.DaySpacingMode switch
        {
            0 => 0.10, // Compact
            2 => 0.36, // Ultra Wide
            _ => (isAnurati ? 0.24 : 0.14) // Wide Mond tracking
        };
        double baseSpacing = fontSize * trackingMultiplier;

        for (int i = 0; i < day.Length; i++)
        {
            char c = day[i];
            double rightMargin = (i == day.Length - 1) ? 0 : baseSpacing;
            double leftMargin = 0;

            // In Anurati, 'I' has an extremely narrow advance width (0.14) which causes it
            // to visually fuse into adjacent characters (especially 'D' in FRIDAY, looking like "FRDAY").
            // Provide explicit breathing margins so 'I' renders cleanly and unmistakably as a distinct glyph.
            if (isAnurati && (c == 'I' || c == 'i'))
            {
                leftMargin = baseSpacing * 0.85;
                rightMargin += baseSpacing * 1.05;
            }

            var tb = new TextBlock
            {
                Text = c.ToString(),
                FontSize = fontSize,
                FontFamily = fontFamily,
                Foreground = brush,
                Margin = new Thickness(leftMargin, 0, rightMargin, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
            TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
            DayLettersPanel.Children.Add(tb);
        }

        // Background card (ultra-clean smoked or cream frosted glass)
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.DayShowBackground, 16, new Thickness(14, 6, 14, 6), _settings.WidgetTheme == 1);
        ToggleCardMenu.Header = _settings.DayShowBackground ? "🔲  Hide Background Card" : "🔲  Show Background Card";
    }

    public void ApplyTheme() => UpdateDayDisplay();

    private System.Windows.Media.Brush GetColorBrush()
    {
        return _settings.DayColorMode switch
        {
            0 => _settings.WidgetTheme == 1
                ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 34, 30, 24))   // Charcoal for light theme
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 255, 255, 255)), // Pure White
            1 => new SolidColorBrush(System.Windows.Media.Color.FromArgb(140, 203, 213, 225)), // Dim Slate
            2 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),        // Electric Cyan
            3 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)),        // Neon Mint
            4 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252)),       // Cyber Violet
            5 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),        // Sunset Orange
            6 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)),        // Amber Gold
            7 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133)),       // Rose Pink
            8 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 63, 94)),         // Crimson Red
            _ => _settings.WidgetTheme == 1
                ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 34, 30, 24))
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 255, 255, 255))
        };
    }

    private System.Windows.Media.FontFamily GetFontFamily()
    {
        return _settings.DayFontMode switch
        {
            1 => BahnschriftFont,
            2 => SegoeUiFont,
            3 => ConsolasFont,
            4 => WidgetStyles.GetAquaticoFont(),
            _ => WidgetStyles.GetAnuratiFont()
        };
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.DayShowBackground = !_settings.DayShowBackground;
        UpdateDayDisplay();
        Save();
    }

    private void SpacingPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.DaySpacingMode = val;
            UpdateDayDisplay();
            Save();
        }
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.DayFontSize = val;
            UpdateDayDisplay();
            Save();
        }
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.DayColorMode = val;
            UpdateDayDisplay();
            Save();
        }
    }

    private void FontPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.DayFontMode = val;
            UpdateDayDisplay();
            Save();
        }
    }

    private void CasePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.DayCaseMode = val;
            UpdateDayDisplay();
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
        _settings.DayEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
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
