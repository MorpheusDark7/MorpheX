using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
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
    private string? _selectedMonitorId;
    private WallpaperInfo? _selectedWallpaper;

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
        RenderDisplayLayout(monitors);
    }

    private void RenderDisplayLayout(IReadOnlyList<MonitorInfo> monitors)
    {
        DisplayLayoutCanvas.Children.Clear();
        if (monitors.Count == 0)
        {
            SelectedDisplayText.Text = "No displays found";
            return;
        }

        if (!monitors.Any(monitor => monitor.DeviceId == _selectedMonitorId))
        {
            _selectedMonitorId = monitors.FirstOrDefault(monitor => monitor.IsPrimary)?.DeviceId ?? monitors[0].DeviceId;
        }

        const double canvasWidth = 480;
        const double canvasHeight = 166;
        const double padding = 12;
        int minX = monitors.Min(monitor => monitor.Bounds.Left);
        int minY = monitors.Min(monitor => monitor.Bounds.Top);
        int maxX = monitors.Max(monitor => monitor.Bounds.Right);
        int maxY = monitors.Max(monitor => monitor.Bounds.Bottom);
        double scale = Math.Min((canvasWidth - 2 * padding) / Math.Max(1, maxX - minX),
            (canvasHeight - 2 * padding) / Math.Max(1, maxY - minY));

        DisplayLayoutCanvas.Width = canvasWidth;
        DisplayLayoutCanvas.Height = canvasHeight;
        for (int index = 0; index < monitors.Count; index++)
        {
            var monitor = monitors[index];
            bool selected = monitor.DeviceId == _selectedMonitorId;
            var active = ((App)Application.Current).WallpaperService.GetActiveWallpaper(monitor.DeviceId);
            var button = new Button
            {
                Tag = monitor.DeviceId,
                Width = Math.Max(62, monitor.Bounds.Width * scale),
                Height = Math.Max(46, monitor.Bounds.Height * scale),
                Background = new SolidColorBrush(selected ? Color.FromRgb(20, 101, 135) : Color.FromRgb(47, 47, 55)),
                BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(56, 189, 248) : Color.FromRgb(95, 95, 105)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                ToolTip = $"{monitor.DisplayName}\n{monitor.Bounds.Width}×{monitor.Bounds.Height}\n" +
                          (active is null ? "No wallpaper assigned" : $"Active: {active.Name}")
            };
            button.Click += DisplayTile_Click;
            button.Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = (index + 1).ToString(), FontSize = 18, FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White },
                    new TextBlock { Text = active is null ? "No wallpaper" : "Assigned", FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White, Opacity = .8 }
                }
            };
            Canvas.SetLeft(button, padding + (monitor.Bounds.Left - minX) * scale);
            Canvas.SetTop(button, padding + (monitor.Bounds.Top - minY) * scale);
            DisplayLayoutCanvas.Children.Add(button);
        }

        UpdateSelectionSummary();
    }

    private void DisplayTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string deviceId })
        {
            _selectedMonitorId = deviceId;
            RenderDisplayLayout(((App)Application.Current).MonitorService.Monitors);
        }
    }

    private void ChooseWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var options = app.LibraryService.Wallpapers
            .Where(wallpaper => wallpaper.Type is WallpaperType.Image or WallpaperType.Video or WallpaperType.AnimatedImage)
            .OrderBy(wallpaper => wallpaper.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (options.Count == 0)
        {
            MessageBox.Show("Add an image, video, or GIF to your library first.", "No Supported Wallpapers",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var picker = new Window
        {
            Title = "Choose Wallpaper",
            Owner = Application.Current.MainWindow,
            Width = 460,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            Foreground = Brushes.White,
            Padding = new Thickness(18)
        };
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var list = new ListBox { ItemsSource = options, DisplayMemberPath = "Name", MinHeight = 340 };
        Grid.SetRow(list, 0);
        panel.Children.Add(list);
        var choose = new Button { Content = "Use selected wallpaper", IsDefault = true, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(10, 7, 10, 7) };
        choose.Click += (_, _) =>
        {
            if (list.SelectedItem is WallpaperInfo wallpaper)
            {
                _selectedWallpaper = wallpaper;
                picker.DialogResult = true;
            }
        };
        Grid.SetRow(choose, 1);
        panel.Children.Add(choose);
        picker.Content = panel;
        if (picker.ShowDialog() == true)
        {
            UpdateSelectionSummary();
        }
    }

    private async void ApplySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedMonitorId) || _selectedWallpaper == null) return;

        ApplySelectedButton.IsEnabled = false;
        try
        {
            await MonitorPickerHelper.ApplyToMonitorAsync(_selectedMonitorId, _selectedWallpaper);
            RefreshDisplays();
        }
        finally
        {
            ApplySelectedButton.IsEnabled = _selectedWallpaper != null;
        }
    }

    private void UpdateSelectionSummary()
    {
        var app = (App)Application.Current;
        var monitor = app.MonitorService.Monitors.FirstOrDefault(item => item.DeviceId == _selectedMonitorId);
        SelectedDisplayText.Text = monitor == null ? "Select a display above" : $"Selected: {monitor.DisplayName}";
        SelectedWallpaperText.Text = _selectedWallpaper == null
            ? "No wallpaper selected"
            : $"Ready to apply: {_selectedWallpaper.Name}";
        ApplySelectedButton.IsEnabled = monitor != null && _selectedWallpaper != null;
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
