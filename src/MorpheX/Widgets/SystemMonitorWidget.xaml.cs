using System.Windows;
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
    private System.Windows.Threading.DispatcherTimer? _saveDebounceTimer;

    public SystemMonitorWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left   = settings.SystemMonitorX;
        Top    = settings.SystemMonitorY;
        Width  = Math.Max(180, settings.SystemMonitorWidth);
        Height = Math.Max(110, settings.SystemMonitorHeight);

        Loaded      += OnLoaded;
        Closing     += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLockMenuHeader();

        var app = (App)Application.Current;
        _metricsService = app.SystemMetricsService;
        _metricsService.MetricsUpdated += OnMetricsUpdated;

        // Ensure service is running
        _metricsService.Start();

        // Render current metrics immediately so it doesn't stay at 0%
        if (_metricsService.CurrentMetrics != null)
        {
            ApplyMetrics(_metricsService.CurrentMetrics);
        }
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
        RamDetailText.Text = $"RAM: {m.RamUsedGb:0.0} / {m.RamTotalGb:0.0} GB";

        UpdateArc(CpuArc, m.CpuPercent);
        UpdateArc(GpuArc, m.GpuPercent);
        UpdateArc(RamArc, m.RamPercent);
    }

    private static void UpdateArc(Path path, double percent, double radius = 23, double cx = 29, double cy = 29)
    {
        percent = Math.Clamp(percent, 0, 99.9);

        if (percent < 0.5)
        {
            path.Data = Geometry.Empty;
            return;
        }

        double angle = (percent / 100.0) * 360.0;
        double angleRad = (angle - 90.0) * Math.PI / 180.0;

        double startX = cx;
        double startY = cy - radius;
        double endX = cx + radius * Math.Cos(angleRad);
        double endY = cy + radius * Math.Sin(angleRad);

        bool isLargeArc = angle > 180.0;

        var figure = new PathFigure
        {
            StartPoint = new System.Windows.Point(startX, startY),
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment(
            new System.Windows.Point(endX, endY),
            new System.Windows.Size(radius, radius),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true));

        var geom = new PathGeometry();
        geom.Figures.Add(figure);
        path.Data = geom;
    }

    private void Widget_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void ResetSize_Click(object sender, RoutedEventArgs e)
    {
        Width  = 260;
        Height = 150;
        _settings.SystemMonitorWidth  = Width;
        _settings.SystemMonitorHeight = Height;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked
            ? "\ud83d\udd13  Unlock Position"
            : "\ud83d\udd12  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.SystemMonitorEnabled = false;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
        Close();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settings.SystemMonitorWidth  = ActualWidth;
        _settings.SystemMonitorHeight = ActualHeight;
        ScheduleDebouncedSave();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_metricsService != null)
            _metricsService.MetricsUpdated -= OnMetricsUpdated;

        _settings.SystemMonitorX      = Left;
        _settings.SystemMonitorY      = Top;
        _settings.SystemMonitorWidth  = ActualWidth;
        _settings.SystemMonitorHeight = ActualHeight;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.SystemMonitorX = Left;
        _settings.SystemMonitorY = Top;
    }

    private void ScheduleDebouncedSave()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            var app = (App)Application.Current;
            _ = app.SettingsService.SaveAsync();
        };
        _saveDebounceTimer.Start();
    }
}
