using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX;

public sealed class DisplayViewModel
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Visibility PrimaryVisibility { get; set; } = Visibility.Collapsed;
    public string SpecsString { get; set; } = string.Empty;
    public string ActiveWallpaperString { get; set; } = string.Empty;

    public bool HasAudioControls { get; set; }
    public bool IsMuted { get; set; }
    public bool IsNotMuted => !IsMuted;
    public int Volume { get; set; }
    public string VolumeText => $"{Volume}%";
    public string MuteTooltip => IsMuted ? "Unmute this display" : "Mute this display";
}

public partial class DisplaysPage : Page
{
    public DisplaysPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshDisplays();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app.MonitorService.Refresh();
        RefreshDisplays();
    }

    private void IdentifyButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Information("Identify displays clicked");
        DisplayIdentifyHelper.ShowIdentifyOverlays();
    }

    public void RefreshDisplays()
    {
        var app = (App)Application.Current;
        var monitors = app.MonitorService.Monitors;

        DisplaysCountText.Text = $"{monitors.Count} display{(monitors.Count != 1 ? "s" : "")} detected";

        var list = new List<DisplayViewModel>();
        foreach (var m in monitors)
        {
            var active = app.WallpaperService.GetActiveWallpaper(m.DeviceId);
            bool hasAudio = active != null && active.Type == WallpaperType.Video;
            bool isMuted = app.WallpaperService.IsMonitorMuted(m.DeviceId);
            int volume = app.WallpaperService.GetMonitorVolume(m.DeviceId);

            list.Add(new DisplayViewModel
            {
                DeviceId = m.DeviceId,
                DisplayName = m.DisplayName,
                PrimaryVisibility = m.IsPrimary ? Visibility.Visible : Visibility.Collapsed,
                SpecsString = $"{m.Bounds.Width}×{m.Bounds.Height} @ {m.RefreshRate}Hz  •  DPI: {(int)(m.DpiScale * 100)}%  •  Pos: ({m.Bounds.X}, {m.Bounds.Y})",
                ActiveWallpaperString = active != null ? $"Active Wallpaper: {active.Name} ({active.Type})" : "Active Wallpaper: None",
                HasAudioControls = hasAudio,
                IsMuted = isMuted,
                Volume = volume
            });
        }

        DisplaysList.ItemsSource = list;
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            bool currentMuted = app.WallpaperService.IsMonitorMuted(deviceId);
            app.WallpaperService.SetMonitorMuted(deviceId, !currentMuted);
            RefreshDisplays();
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            int newVolume = (int)Math.Round(e.NewValue);
            app.WallpaperService.SetMonitorVolume(deviceId, newVolume);

            if (element.Parent is Panel panel)
            {
                var textBlock = panel.Children.OfType<TextBlock>().LastOrDefault();
                if (textBlock != null)
                {
                    textBlock.Text = $"{newVolume}%";
                }
            }
        }
    }

    private async void ClearWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            try
            {
                await app.WallpaperService.RemoveWallpaperAsync(deviceId);
                RefreshDisplays();
                Log.Information("Cleared wallpaper for display {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear wallpaper for display {DeviceId}", deviceId);
            }
        }
    }
}
