using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
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

        ClockToggle.IsChecked        = w.ClockEnabled;
        ClockFormatToggle.IsChecked  = w.ClockIs24Hour;
        SysMonToggle.IsChecked       = w.SystemMonitorEnabled;
        MediaToggle.IsChecked        = w.MediaEnabled;
        DayToggle.IsChecked          = w.DayEnabled;
        CalendarToggle.IsChecked     = w.CalendarEnabled;
        NotesToggle.IsChecked        = w.NotesEnabled;
        QuoteToggle.IsChecked        = w.QuoteEnabled;
        LockWidgetsToggle.IsChecked  = w.WidgetsLocked;
        TaskbarStatsToggle.IsChecked = w.TaskbarStatsEnabled;

        SelectComboItem(VisualizerStyleCombo, w.VisualizerStyle);
        SelectComboItem(VisualizerColorCombo, w.VisualizerColorMode);

        _isInitializing = false;
    }

    private static void SelectComboItem(ComboBox combo, int value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag is string s && int.TryParse(s, out int v) && v == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    // ── Toggle handlers ──────────────────────────────────────────────────────

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

    private async void DayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.DayEnabled = DayToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void CalendarToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.CalendarEnabled = CalendarToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void NotesToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.NotesEnabled = NotesToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void QuoteToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.QuoteEnabled = QuoteToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void TaskbarStatsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.TaskbarStatsEnabled = TaskbarStatsToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void LockWidgetsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.WidgetsLocked = LockWidgetsToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void VisualizerStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (VisualizerStyleCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string s && int.TryParse(s, out int styleVal))
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Widgets.VisualizerStyle = styleVal;
            app.WidgetService.RefreshMedia();
            await app.SettingsService.SaveAsync();
        }
    }

    private async void VisualizerColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (VisualizerColorCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string s && int.TryParse(s, out int colVal))
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Widgets.VisualizerColorMode = colVal;
            app.WidgetService.RefreshMedia();
            await app.SettingsService.SaveAsync();
        }
    }
}
