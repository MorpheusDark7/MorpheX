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

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();

        Closing += OnClosing;
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
        if (e.ClickCount == 2)
        {
            // Double click toggles 12h / 24h format
            _settings.ClockIs24Hour = !_settings.ClockIs24Hour;
            Tick();
            var app = (App)Application.Current;
            _ = app.SettingsService.SaveAsync();
            return;
        }

        DragMove();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _settings.ClockX = Left;
        _settings.ClockY = Top;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.ClockX = Left;
        _settings.ClockY = Top;
    }
}
