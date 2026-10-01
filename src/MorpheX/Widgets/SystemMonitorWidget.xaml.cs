using System.Windows;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class SystemMonitorWidget : Window
{
    private readonly WidgetSettings _settings;
    private ISystemMetricsService? _metricsService;
    // Track bar container width for proportional fill
    private double _barContainerWidth = 0;

    public SystemMonitorWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.SystemMonitorX;
        Top  = settings.SystemMonitorY;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Reuse the already-running SystemMetricsService — zero extra polling
        var app = (App)Application.Current;
        _metricsService = app.SystemMetricsService;
        _metricsService.MetricsUpdated += OnMetricsUpdated;

        // Ensure service is running (it starts with the main window, but may be stopped)
        _metricsService.Start();

        // Measure bar container after layout pass
        Dispatcher.InvokeAsync(() =>
        {
            _barContainerWidth = CpuBar.ActualWidth + ((System.Windows.FrameworkElement)CpuBar.Parent).ActualWidth - CpuBar.ActualWidth;
            // Get parent Border's actual width
            if (CpuBar.Parent is System.Windows.FrameworkElement parent)
                _barContainerWidth = parent.ActualWidth;
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Dispatcher.InvokeAsync(() =>
        {
            CpuText.Text = $"{m.CpuPercent:0}%";
            GpuText.Text = $"{m.GpuPercent:0}%";
            RamText.Text = $"{m.RamPercent:0}%";
            RamDetailText.Text = $"{m.RamUsedGb:0.0} / {m.RamTotalGb:0.0} GB";

            double bw = _barContainerWidth > 0 ? _barContainerWidth : 130;
            CpuBar.Width = bw * (m.CpuPercent / 100.0);
            GpuBar.Width = bw * (m.GpuPercent / 100.0);
            RamBar.Width = bw * (m.RamPercent / 100.0);
        });
    }

    private void Widget_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_metricsService != null)
            _metricsService.MetricsUpdated -= OnMetricsUpdated;

        _settings.SystemMonitorX = Left;
        _settings.SystemMonitorY = Top;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.SystemMonitorX = Left;
        _settings.SystemMonitorY = Top;
    }
}
