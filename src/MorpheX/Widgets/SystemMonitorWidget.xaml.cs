using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Application = System.Windows.Application;

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

        ApplySize();
        UpdateLockMenuHeader();

        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

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
        CpuText.Text = $"{m.CpuPercent:0}%";
        GpuText.Text = $"{m.GpuPercent:0}%";
        RamText.Text = $"{m.RamPercent:0}%";
        RamUsedText.Text = $"{m.RamUsedGb:0.0}";

        UpdateArc(CpuArc, m.CpuPercent);
        UpdateArc(GpuArc, m.GpuPercent);
        UpdateArc(RamArc, m.RamPercent);
    }

    private static void UpdateArc(Path path, double percent, double radius = 23, double cx = 26, double cy = 26)
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

    private void ApplySize()
    {
        double gridSize = _settings.SysMonSize switch { 0 => 40, 2 => 64, _ => 52 };
        double fontSize = _settings.SysMonSize switch { 0 => 9,  2 => 14, _ => 11 };
        double labelSize = _settings.SysMonSize switch { 0 => 8, 2 => 12, _ => 10 };

        foreach (var panel in new[] { CpuPanel, GpuPanel, RamPanel, RamGbPanel })
        {
            foreach (var child in panel.Children)
            {
                if (child is Grid g)
                {
                    g.Width  = gridSize;
                    g.Height = gridSize;
                    // Update ellipses inside
                    foreach (var gc in g.Children)
                    {
                        if (gc is Ellipse el)
                        {
                            el.Width  = gridSize - 6;
                            el.Height = gridSize - 6;
                        }
                        else if (gc is TextBlock tb)
                        {
                            tb.FontSize = fontSize;
                        }
                        else if (gc is StackPanel sp)
                        {
                            foreach (var spc in sp.Children)
                                if (spc is TextBlock stb) stb.FontSize = fontSize;
                        }
                    }
                }
                else if (child is TextBlock ltb)
                {
                    ltb.FontSize = labelSize;
                }
            }
        }
    }

    private void Widget_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void SizeSmall_Click(object sender, RoutedEventArgs e)  { _settings.SysMonSize = 0; ApplySize(); Save(); }
    private void SizeMedium_Click(object sender, RoutedEventArgs e) { _settings.SysMonSize = 1; ApplySize(); Save(); }
    private void SizeLarge_Click(object sender, RoutedEventArgs e)  { _settings.SysMonSize = 2; ApplySize(); Save(); }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        Save();
    }

    private void UpdateLockMenuHeader()
    {
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
