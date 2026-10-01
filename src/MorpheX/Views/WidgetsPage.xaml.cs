using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;

namespace MorpheX;

public partial class WidgetsPage : Page
{
    private bool _isInitializing = true;

    public WidgetsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        ClockToggle.IsChecked = w.ClockEnabled;
        ClockFormatToggle.IsChecked = w.ClockIs24Hour;
        SysMonToggle.IsChecked = w.SystemMonitorEnabled;
        MediaToggle.IsChecked = w.MediaEnabled;

        _isInitializing = false;
    }

    private async void ClockToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.ClockEnabled = ClockToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void ClockFormatToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.ClockIs24Hour = ClockFormatToggle.IsChecked == true;
        app.WidgetService.RefreshClock();
        await app.SettingsService.SaveAsync();
    }

    private async void SysMonToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.SystemMonitorEnabled = SysMonToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void MediaToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.MediaEnabled = MediaToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }
}
