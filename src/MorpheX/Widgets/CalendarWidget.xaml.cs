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

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, true, cornerRadius: 12, isLightTheme: isLight);
        MonthYearText.Foreground = isLight
            ? new SolidColorBrush(Color.FromArgb(200, 34, 30, 24))
            : new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));

        if (ThemeMenuItem != null)
        {
            ThemeMenuItem.Header = isLight ? "🌙  Dark Theme" : "☀️  Light Theme";
        }

        BuildDayHeaders();
        RenderMonth(DateTime.Now);
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetTheme = _settings.WidgetTheme == 0 ? 1 : 0;
        ApplyTheme();
        Save();
        (Application.Current as App)?.WidgetService.RefreshTheme();
    }

    private void BuildDayHeaders()
    {
        bool isLight = _settings.WidgetTheme == 1;
        DayHeaders.Children.Clear();
        var headerBrush = isLight
            ? new SolidColorBrush(Color.FromArgb(140, 60, 50, 40))
            : new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));

        foreach (var d in new[] { "Su","Mo","Tu","We","Th","Fr","Sa" })
        {
            DayHeaders.Children.Add(new TextBlock
            {
                Text = d, FontSize = 9, FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = headerBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center, Width = 26
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
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        for (int i = 0; i < startDow; i++) DaysGrid.Children.Add(new TextBlock());

        var todayBg = isLight
            ? new SolidColorBrush(Color.FromRgb(30, 34, 42))     // Dark slate pill for light mode
            : new SolidColorBrush(Colors.White);                 // Pure white pill for dark mode
        var todayFg = isLight
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(Color.FromRgb(14, 14, 18));

        var regularFg = isLight
            ? new SolidColorBrush(Color.FromArgb(215, 34, 30, 24))
            : new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        var weekendFg = isLight
            ? new SolidColorBrush(Color.FromArgb(130, 80, 70, 60))
            : new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));

        for (int day = 1; day <= daysInMonth; day++)
        {
            if (day == now.Day)
            {
                var b = new Border { Width=22, Height=22, CornerRadius=new CornerRadius(11),
                    Background=todayBg,
                    HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
                    Margin=new Thickness(2) };
                b.Child = new TextBlock { Text=day.ToString(), FontSize=10, FontWeight=FontWeights.Bold,
                    FontFamily=new FontFamily("Segoe UI"),
                    Foreground=todayFg,
                    HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
                    TextAlignment=TextAlignment.Center };
                DaysGrid.Children.Add(b);
            }
            else
            {
                bool wend = ((startDow+day-1)%7==0)||((startDow+day-1)%7==6);
                DaysGrid.Children.Add(new TextBlock { Text=day.ToString(), FontSize=10,
                    FontFamily=new FontFamily("Segoe UI"),
                    Foreground=wend ? weekendFg : regularFg,
                    HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
                    TextAlignment=TextAlignment.Center, Width=26, Margin=new Thickness(2,3,2,3) });
            }
        }
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (_settings.WidgetsLocked) return; DragMove(); }

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
    { _settings.CalendarEnabled = false; Save(); Close(); }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    { _settings.CalendarX = Left; _settings.CalendarY = Top; Save(); }

    protected override void OnLocationChanged(EventArgs e)
    { base.OnLocationChanged(e); _settings.CalendarX = Left; _settings.CalendarY = Top; }

    private void Save()
    { var app = (App)Application.Current; _ = app.SettingsService.SaveAsync(); }
}