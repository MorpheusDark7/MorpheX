using System.Windows;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class MediaWidget : Window
{
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _timer;

    public MediaWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.MediaX;
        Top  = settings.MediaY;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshState();
        _timer.Start();
        RefreshState();

        Closing += OnClosing;
    }

    private void RefreshState()
    {
        var app = (App)Application.Current;
        var active = app.WallpaperService?.GetAllActiveWallpapers();
        bool paused = app.PlaybackService?.IsManuallyPaused ?? false;

        if (active == null || active.Count == 0)
        {
            WallpaperNameText.Text = "No wallpaper active";
            WallpaperTypeText.Text = "";
        }
        else
        {
            WallpaperNameText.Text = active[0].Name;
            WallpaperTypeText.Text = active[0].Type.ToString();
        }

        PauseResumeBtn.Content = paused ? "▶" : "⏸";
    }

    private void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app.PlaybackService == null) return;
        if (app.PlaybackService.IsManuallyPaused)
            app.PlaybackService.Resume();
        else
            app.PlaybackService.Pause();
        RefreshState();
    }

    private void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        _ = app.PlaylistService?.TriggerNextWallpaperAsync();
    }

    private void Widget_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        _settings.MediaX = Left;
        _settings.MediaY = Top;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.MediaX = Left;
        _settings.MediaY = Top;
    }
}
