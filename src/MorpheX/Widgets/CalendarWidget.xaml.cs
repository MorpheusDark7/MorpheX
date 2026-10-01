using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

        UpdateLockMenuHeader();
        BuildDayHeaders();
        RenderMonth(DateTime.Now);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Month != _lastRenderedMonth)
                RenderMonth(now);
        };
        timer.Start();

        Closing += OnClosing;
    }

    private void BuildDayHeaders()
    {
        DayHeaders.Children.Clear();
        var days = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        foreach (var d in days)
        {
            DayHeaders.Children.Add(new TextBlock
            {
                Text = d,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = 26
            });
        }
    }

    private void RenderMonth(DateTime now)
    {
        _lastRenderedMonth = now.Month;
        MonthYearText.Text = now.ToString("MMMM yyyy").ToUpperInvariant();

        DaysGrid.Children.Clear();

        var firstDay = new DateTime(now.Year, now.Month, 1);
        int startDow   = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        for (int i = 0; i < startDow; i++)
            DaysGrid.Children.Add(new TextBlock());

        for (int day = 1; day <= daysInMonth; day++)
        {
            bool isToday = day == now.Day;

            if (isToday)
            {
                var border = new Border
                {
                    Width = 22, Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2)
                };
                border.Child = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(Color.FromRgb(14, 14, 18)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                DaysGrid.Children.Add(border);
            }
            else
            {
                bool isWeekend = ((startDow + day - 1) % 7 == 0) || ((startDow + day - 1) % 7 == 6);
                DaysGrid.Children.Add(new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = 10,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(Color.FromArgb(
                        isWeekend ? (byte)140 : (byte)200, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Width = 26,
                    Margin = new Thickness(2, 3, 2, 3)
                });
            }
        }
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        Save();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked ? "\ud83d\udd13  Unlock Position" : "\ud83d\udd12  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.CalendarEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.CalendarX = Left;
        _settings.CalendarY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.CalendarX = Left;
        _settings.CalendarY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
