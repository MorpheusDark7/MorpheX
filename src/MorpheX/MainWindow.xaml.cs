using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Wpf.Ui.Controls;

namespace MorpheX;

public partial class MainWindow : FluentWindow
{
    private readonly Dictionary<Type, Page> _pageCache = new();
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        NavigationView.SelectionChanged += NavigationView_SelectionChanged;

        Loaded += MainWindow_Loaded;

        StateChanged += MainWindow_StateChanged;

        var app = (App)System.Windows.Application.Current;
        app.SystemMetricsService.MetricsUpdated += OnMetricsUpdated;

        NavigateTo(typeof(LibraryPage));
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        if (WindowState == WindowState.Minimized)
        {
            app.SystemMetricsService.Stop();
            Hide();
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
        else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
        {
            if (app.SettingsService.Settings.Appearance.ShowSystemStats)
            {
                app.SystemMetricsService.Start();
            }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ClampToWorkArea();
        UpdateStatsBarVisibility();
    }

    public void UpdateStatsBarVisibility()
    {
        var app = (App)System.Windows.Application.Current;
        bool show = app.SettingsService.Settings.Appearance.ShowSystemStats;
        SystemStatsBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        if (show && IsVisible && WindowState != WindowState.Minimized)
        {
            app.SystemMetricsService.Start();
        }
        else
        {
            app.SystemMetricsService.Stop();
        }
    }

    private void OnMetricsUpdated(object? sender, MorpheX.Core.Services.SystemMetrics metrics)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatsCpuText.Text = $"{metrics.CpuPercent:0.0}%";
            StatsGpuText.Text = $"{metrics.GpuPercent:0.0}%";
            StatsRamText.Text = $"{metrics.RamUsedGb:0.0} / {metrics.RamTotalGb:0.0} GB ({metrics.RamPercent:0}%)";
            StatsAppRamText.Text = $"{metrics.AppWorkingSetMb} MB";
        });
    }

    private void ClampToWorkArea()
    {
        var hwndSource = PresentationSource.FromVisual(this);
        if (hwndSource == null) return;

        var dpiScaleX = hwndSource.CompositionTarget.TransformToDevice.M11;
        var dpiScaleY = hwndSource.CompositionTarget.TransformToDevice.M22;

        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        var workArea = screen.WorkingArea;

        double availableWidth = workArea.Width / dpiScaleX;
        double availableHeight = workArea.Height / dpiScaleY;

        if (Height > availableHeight)
        {
            Height = availableHeight;
        }
        if (Width > availableWidth)
        {
            Width = availableWidth;
        }

        double workLeft = workArea.Left / dpiScaleX;
        double workTop = workArea.Top / dpiScaleY;

        if (Top < workTop) Top = workTop;
        if (Left < workLeft) Left = workLeft;
        if (Top + Height > workTop + availableHeight)
        {
            Top = workTop + availableHeight - Height;
        }
        if (Left + Width > workLeft + availableWidth)
        {
            Left = workLeft + availableWidth - Width;
        }
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    public void NavigateTo(Type pageType)
    {
        if (!_pageCache.TryGetValue(pageType, out var page))
        {
            page = (Page)Activator.CreateInstance(pageType)!;
            _pageCache[pageType] = page;
        }

        if (page is LibraryPage libPage)
        {
            libPage.RefreshWallpaperList();
        }
        else if (page is FavoritesPage favPage)
        {
            favPage.RefreshFavorites();
        }
        else if (page is DisplaysPage dispPage)
        {
            dispPage.RefreshDisplays();
        }

        ContentFrame.Navigate(page);

        SelectNavigationItem(pageType);
    }

    private void SelectNavigationItem(Type pageType)
    {
        NavLibrary.IsActive = (pageType == typeof(LibraryPage));
        NavFavorites.IsActive = (pageType == typeof(FavoritesPage));
        NavDisplays.IsActive = (pageType == typeof(DisplaysPage));
        NavSettings.IsActive = (pageType == typeof(SettingsPage));
    }

    private void NavigationView_SelectionChanged(NavigationView sender,
        System.Windows.RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem item && item.TargetPageType != null)
        {
            NavigateTo(item.TargetPageType);
        }
    }

    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();

        UpdateStatsBarVisibility();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            var app = (App)System.Windows.Application.Current;
            app.SystemMetricsService.Stop();
            Hide();
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
            return;
        }

        base.OnClosing(e);
    }
}
