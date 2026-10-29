using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using MorpheX.Core.Configuration;
using MorpheX.Services;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class CalendarWidget : Window
{
    private readonly WidgetSettings _settings;
    private int _lastRenderedMonth = -1;

    public CalendarWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        Left = settings.CalendarX;
        Top  = settings.CalendarY;

        ApplyTheme();
        UpdateLockState();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (_, _) => { var n = DateTime.Now; if (n.Month != _lastRenderedMonth) RenderMonth(n); };
        timer.Start();
        Closing += OnClosing;
    }

    public void RefreshDisplay() => ApplyTheme();

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.CalendarShowBackground, cornerRadius: 12, isLightTheme: isLight);
        MonthYearText.Foreground = isLight
            ? new SolidColorBrush(Color.FromArgb(220, 34, 30, 24))
            : new SolidColorBrush(Color.FromArgb(215, 255, 255, 255));

        Opacity = Math.Clamp(_settings.CalendarOpacity > 0 ? _settings.CalendarOpacity : 1.0, 0.2, 1.0);
        WidgetWindowHelper.RefreshWidgetSurface(this);

        ToggleCardMenu.Header = _settings.CalendarShowBackground ? "Hide Background Card" : "Show Background Card";
        ToggleWeekStartMenu.Header = _settings.CalendarStartOnMonday ? "Start Week on Sunday" : "Start Week on Monday";

        BuildDayHeaders();
        RenderMonth(DateTime.Now);
    }

    private void BuildDayHeaders()
    {
        bool isLight = _settings.WidgetTheme == 1;
        DayHeaders.Children.Clear();
        var headerBrush = isLight
            ? new SolidColorBrush(Color.FromArgb(160, 60, 50, 40))
            : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));

        var days = _settings.CalendarStartOnMonday
            ? new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" }
            : new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

        double cellWidth = _settings.CalendarFontSize switch { 0 => 22, 2 => 32, _ => 26 };
        double fsize = _settings.CalendarFontSize switch { 0 => 8, 2 => 11, _ => 9 };

        foreach (var d in days)
        {
            DayHeaders.Children.Add(new TextBlock
            {
                Text = d, FontSize = fsize, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = headerBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center, Width = cellWidth
            });
        }
    }

    private void RenderMonth(DateTime now)
    {
        bool isLight = _settings.WidgetTheme == 1;
        _lastRenderedMonth = now.Month;
        MonthYearText.Text = now.ToString("MMMM yyyy").ToUpperInvariant();
        DaysGrid.Children.Clear();

        var firstDay = new DateTime(now.Year, now.Month, 1);
        int startDow = (int)firstDay.DayOfWeek;
        if (_settings.CalendarStartOnMonday)
        {
            startDow = (startDow + 6) % 7; // Sunday becomes 6, Monday becomes 0
        }

        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        for (int i = 0; i < startDow; i++) DaysGrid.Children.Add(new TextBlock());

        var todayBg = GetTodayPillBrush(isLight);
        var todayFg = (isLight && _settings.CalendarColorMode == 0)
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(Color.FromRgb(14, 14, 18));

        var regularFg = isLight
            ? new SolidColorBrush(Color.FromArgb(235, 34, 30, 24))
            : new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
        var weekendFg = isLight
            ? new SolidColorBrush(Color.FromArgb(150, 80, 70, 60))
            : new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));

        double pillSize = _settings.CalendarFontSize switch { 0 => 18, 2 => 26, _ => 22 };
        double fsize = _settings.CalendarFontSize switch { 0 => 9, 2 => 12, _ => 10 };
        double cellWidth = _settings.CalendarFontSize switch { 0 => 22, 2 => 32, _ => 26 };

        for (int day = 1; day <= daysInMonth; day++)
        {
            if (day == now.Day)
            {
                var b = new Border
                {
                    Width = pillSize, Height = pillSize, CornerRadius = new CornerRadius(pillSize / 2),
                    Background = todayBg,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2)
                };
                b.Child = new TextBlock
                {
                    Text = day.ToString(), FontSize = fsize, FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = todayFg,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                DaysGrid.Children.Add(b);
            }
            else
            {
                int col = (startDow + day - 1) % 7;
                bool isWeekend = _settings.CalendarStartOnMonday ? (col == 5 || col == 6) : (col == 0 || col == 6);
                DaysGrid.Children.Add(new TextBlock
                {
                    Text = day.ToString(), FontSize = fsize,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = isWeekend ? weekendFg : regularFg,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center, Width = cellWidth, Margin = new Thickness(2, 3, 2, 3)
                });
            }
        }
    }

    private SolidColorBrush GetTodayPillBrush(bool isLight)
    {
        return _settings.CalendarColorMode switch
        {
            1 => new SolidColorBrush(Color.FromRgb(56, 189, 248)),  // Neon Cyan
            2 => new SolidColorBrush(Color.FromRgb(74, 222, 128)),  // Emerald Mint
            3 => new SolidColorBrush(Color.FromRgb(192, 132, 252)), // Cyber Violet
            4 => new SolidColorBrush(Color.FromRgb(251, 146, 60)),  // Sunset Amber
            5 => new SolidColorBrush(Color.FromRgb(251, 113, 133)), // Rose Pink
            _ => isLight ? new SolidColorBrush(Color.FromRgb(30, 34, 42)) : new SolidColorBrush(Colors.White)
        };
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.CalendarShowBackground = !_settings.CalendarShowBackground;
        ApplyTheme();
        Save();
    }

    private void ToggleWeekStart_Click(object sender, RoutedEventArgs e)
    {
        _settings.CalendarStartOnMonday = !_settings.CalendarStartOnMonday;
        ApplyTheme();
        Save();
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int col))
        {
            _settings.CalendarColorMode = col;
            ApplyTheme();
            Save();
        }
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int sz))
        {
            _settings.CalendarFontSize = sz;
            ApplyTheme();
            Save();
        }
    }

    private void Opacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op))
        {
            _settings.CalendarOpacity = op;
            ApplyTheme();
            Save();
        }
    }

    private void ReloadWidget_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app?.WidgetService.ReloadWidget(typeof(CalendarWidget));
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (_settings.WidgetsLocked) return; DragMove(); Save(); }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        Save();
        (Application.Current as App)?.WidgetService.RefreshLockState();
    }

    public void Reposition()
    {
        Left = _settings.CalendarX;
        Top = _settings.CalendarY;
        ApplyTheme();
        UpdateLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    { _settings.CalendarEnabled = false; Save(); Close(); }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    { _settings.CalendarX = Left; _settings.CalendarY = Top; Save(); }

    protected override void OnLocationChanged(EventArgs e)
    { base.OnLocationChanged(e); _settings.CalendarX = Left; _settings.CalendarY = Top; }

    private void Save()
    { var app = (App)Application.Current; _ = app.SettingsService.SaveAsync(); }
}
