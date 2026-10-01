using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;
using StackPanel = System.Windows.Controls.StackPanel;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;

namespace MorpheX.Widgets;

public partial class SystemMonitorWidget : Window
{
    private readonly WidgetSettings _settings;
    private ISystemMetricsService? _metricsService;

    public SystemMonitorWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.SystemMonitorX;
        Top  = settings.SystemMonitorY;

        ApplyStylingAndLayout();
        UpdateLockState();

        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

    public void RefreshDisplay() => ApplyStylingAndLayout();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        _metricsService = app.SystemMetricsService;
        _metricsService.MetricsUpdated += OnMetricsUpdated;
        _metricsService.Start();

        if (_metricsService.CurrentMetrics != null)
            ApplyMetrics(_metricsService.CurrentMetrics);
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Dispatcher.InvokeAsync(() => ApplyMetrics(m));
    }

    private void ApplyMetrics(SystemMetrics m)
    {
        CpuText.Text  = $"{m.CpuPercent:0}%";
        RamText.Text  = $"{m.RamPercent:0}%";
        GpuText.Text  = $"{m.GpuPercent:0}%";
        DiskText.Text = $"{m.DiskPercent:0}%";

        double gridSize = GetGridSize();
        double radius = (gridSize - 6) / 2.0;
        double center = gridSize / 2.0;

        UpdateArc(CpuArc, m.CpuPercent, radius, center, center);
        UpdateArc(RamArc, m.RamPercent, radius, center, center);
        UpdateArc(GpuArc, m.GpuPercent, radius, center, center);
        UpdateArc(DiskArc, m.DiskPercent, radius, center, center);
    }

    private static void UpdateArc(Path path, double percent, double radius, double cx, double cy)
    {
        percent = Math.Clamp(percent, 0, 99.9);
        if (percent < 0.5) { path.Data = Geometry.Empty; return; }

        double angle    = percent / 100.0 * 360.0;
        double angleRad = (angle - 90.0) * Math.PI / 180.0;

        double startX = cx, startY = cy - radius;
        double endX   = cx + radius * Math.Cos(angleRad);
        double endY   = cy + radius * Math.Sin(angleRad);

        var figure = new PathFigure
        {
            StartPoint = new System.Windows.Point(startX, startY),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment(
            new System.Windows.Point(endX, endY),
            new System.Windows.Size(radius, radius),
            0, angle > 180.0, SweepDirection.Clockwise, true));

        var geom = new PathGeometry();
        geom.Figures.Add(figure);
        path.Data = geom;
    }

    private double GetGridSize()
    {
        return _settings.SysMonSize switch
        {
            0 => 44,
            1 => 56,
            2 => 72,
            3 => 90,
            _ => 56
        };
    }

    public void ApplyStylingAndLayout()
    {
        double gridSize = GetGridSize();
        double ringSize = gridSize - 6;
        double fontSize = _settings.SysMonSize switch { 0 => 10, 1 => 12, 2 => 15, 3 => 18, _ => 12 };
        double labelSize = _settings.SysMonSize switch { 0 => 9, 1 => 10, 2 => 12, 3 => 14, _ => 10 };
        double thickness = _settings.SysMonSize switch { 0 => 2.8, 1 => 3.5, 2 => 4.2, 3 => 5.0, _ => 3.5 };

        var (arcBrush, trackBrush, textBrush, labelBrush, glow) = GetColorScheme();

        // 1. Rebuild CirclesHost panel layout
        Panel newHost = _settings.SysMonOrientation switch
        {
            1 => new StackPanel { Orientation = Orientation.Vertical },
            2 => new UniformGrid { Columns = 2 },
            _ => new StackPanel { Orientation = Orientation.Horizontal }
        };

        // Detach panels from current container
        if (CardBorder.Child is Panel oldPanel)
        {
            oldPanel.Children.Clear();
        }

        CardBorder.Child = newHost;

        // 2. Configure individual panels
        var pairs = new[]
        {
            (CpuPanel,  CpuGrid,  CpuTrack,  CpuArc,  CpuText,  CpuLabel,  _settings.SysMonShowCpu),
            (RamPanel,  RamGrid,  RamTrack,  RamArc,  RamText,  RamLabel,  _settings.SysMonShowRam),
            (GpuPanel,  GpuGrid,  GpuTrack,  GpuArc,  GpuText,  GpuLabel,  _settings.SysMonShowGpu),
            (DiskPanel, DiskGrid, DiskTrack, DiskArc, DiskText, DiskLabel, _settings.SysMonShowDisk)
        };

        foreach (var (panel, grid, track, arc, text, label, visible) in pairs)
        {
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
            {
                newHost.Children.Add(panel);
            }

            grid.Width  = gridSize;
            grid.Height = gridSize;

            track.Width  = ringSize;
            track.Height = ringSize;
            track.Stroke = trackBrush;
            track.StrokeThickness = thickness;

            arc.Stroke = arcBrush;
            arc.StrokeThickness = thickness;
            arc.Effect = glow;

            text.FontSize = fontSize;
            text.Foreground = textBrush;

            label.FontSize = labelSize;
            label.Foreground = labelBrush;
        }

        // 3. Background Card Styling (translucent frosted smoked acrylic)
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.SysMonShowBackground, 16, _settings.SysMonShowBackground ? new Thickness(10) : new Thickness(4));

        // Update menu checkmarks
        ToggleCardMenu.Header = _settings.SysMonShowBackground ? "🔲  Hide Background Card" : "🔲  Show Background Card";
        ToggleCpuMenu.IsChecked  = _settings.SysMonShowCpu;
        ToggleRamMenu.IsChecked  = _settings.SysMonShowRam;
        ToggleGpuMenu.IsChecked  = _settings.SysMonShowGpu;
        ToggleDiskMenu.IsChecked = _settings.SysMonShowDisk;

        // Force arc redraw with current metrics
        if (_metricsService?.CurrentMetrics != null)
            ApplyMetrics(_metricsService.CurrentMetrics);
    }

    private (System.Windows.Media.Brush arc, System.Windows.Media.Brush track, System.Windows.Media.Brush text, System.Windows.Media.Brush label, DropShadowEffect? glow) GetColorScheme()
    {
        return _settings.SysMonColorMode switch
        {
            1 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 56, 189, 248)),
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 242, 254)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 56, 189, 248)),
                new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Color = System.Windows.Media.Color.FromRgb(56, 189, 248), Opacity = 0.6 }
            ),
            2 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 74, 222, 128)),
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 74, 222, 128)),
                new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Color = System.Windows.Media.Color.FromRgb(74, 222, 128), Opacity = 0.6 }
            ),
            3 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 168, 85, 247)),
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 232, 255)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 168, 85, 247)),
                new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Color = System.Windows.Media.Color.FromRgb(168, 85, 247), Opacity = 0.6 }
            ),
            4 => (
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 251, 146, 60)),
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 237, 213)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 251, 146, 60)),
                new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Color = System.Windows.Media.Color.FromRgb(251, 146, 60), Opacity = 0.6 }
            ),
            _ => (
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 255, 255, 255)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 255, 255, 255)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 255, 255, 255)),
                new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Color = System.Windows.Media.Color.FromRgb(255, 255, 255), Opacity = 0.35 }
            )
        };
    }

    private void Widget_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.SysMonSize = val;
            ApplyStylingAndLayout();
            Save();
        }
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.SysMonColorMode = val;
            ApplyStylingAndLayout();
            Save();
        }
    }

    private void LayoutPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && int.TryParse(item.Tag?.ToString(), out int val))
        {
            _settings.SysMonOrientation = val;
            ApplyStylingAndLayout();
            Save();
        }
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.SysMonShowBackground = !_settings.SysMonShowBackground;
        ApplyStylingAndLayout();
        Save();
    }

    private void ToggleCpu_Click(object sender, RoutedEventArgs e)
    {
        _settings.SysMonShowCpu = !_settings.SysMonShowCpu;
        ApplyStylingAndLayout();
        Save();
    }

    private void ToggleRam_Click(object sender, RoutedEventArgs e)
    {
        _settings.SysMonShowRam = !_settings.SysMonShowRam;
        ApplyStylingAndLayout();
        Save();
    }

    private void ToggleGpu_Click(object sender, RoutedEventArgs e)
    {
        _settings.SysMonShowGpu = !_settings.SysMonShowGpu;
        ApplyStylingAndLayout();
        Save();
    }

    private void ToggleDisk_Click(object sender, RoutedEventArgs e)
    {
        _settings.SysMonShowDisk = !_settings.SysMonShowDisk;
        ApplyStylingAndLayout();
        Save();
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
        _settings.SystemMonitorEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_metricsService != null)
            _metricsService.MetricsUpdated -= OnMetricsUpdated;

        _settings.SystemMonitorX = Left;
        _settings.SystemMonitorY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.SystemMonitorX = Left;
        _settings.SystemMonitorY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
