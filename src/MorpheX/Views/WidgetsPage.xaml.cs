using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Application = System.Windows.Application;
using MorpheX.Services;

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

        LockWidgetsToggle.IsChecked = w.WidgetsLocked;
        SelectComboItem(WidgetThemeCombo, w.WidgetTheme);

        // Clock
        ClockToggle.IsChecked       = w.ClockEnabled;
        ClockFormatCheck.IsChecked  = w.ClockIs24Hour;
        ClockDateCheck.IsChecked    = w.ClockShowDate;
        ClockCardCheck.IsChecked    = w.ClockShowBackground;
        SelectComboItem(ClockSizeCombo,  w.ClockFontSize);
        SelectComboItem(ClockColorCombo, w.ClockColorMode);
        SelectComboItem(ClockFontCombo,  w.ClockFontMode);

        // Day Name
        DayToggle.IsChecked     = w.DayEnabled;
        DayCardCheck.IsChecked  = w.DayShowBackground;
        SelectComboItem(DaySizeCombo,    w.DayFontSize);
        SelectComboItem(DayColorCombo,   w.DayColorMode);
        SelectComboItem(DayFontCombo,    w.DayFontMode);
        SelectComboItem(DayCaseCombo,    w.DayCaseMode);
        SelectComboItem(DaySpacingCombo, w.DaySpacingMode);

        // System Monitor (Simplicity Circles)
        SysMonToggle.IsChecked    = w.SystemMonitorEnabled;
        SysMonCardCheck.IsChecked = w.SysMonShowBackground;
        SysMonCpuCheck.IsChecked  = w.SysMonShowCpu;
        SysMonRamCheck.IsChecked  = w.SysMonShowRam;
        SysMonGpuCheck.IsChecked  = w.SysMonShowGpu;
        SysMonDiskCheck.IsChecked = w.SysMonShowDisk;
        SelectComboItem(SysMonSizeCombo,   w.SysMonSize);
        SelectComboItem(SysMonColorCombo,  w.SysMonColorMode);
        SelectComboItem(SysMonLayoutCombo, w.SysMonOrientation);

        // Media & Visualizer
        MediaToggle.IsChecked       = w.MediaEnabled;
        MediaCardCheck.IsChecked    = w.MediaShowBackground;
        MediaDetailsCheck.IsChecked = w.MediaShowTrackDetails;
        MediaGlowCheck.IsChecked    = w.VisualizerGlow;
        SelectComboItem(VisualizerStyleCombo, w.VisualizerStyle);
        SelectComboItem(VisualizerColorCombo, w.VisualizerColorMode);

        // Other widgets
        CalendarToggle.IsChecked = w.CalendarEnabled;
        NotesToggle.IsChecked    = w.NotesEnabled;
        QuoteToggle.IsChecked    = w.QuoteEnabled;

        // Taskbar
        SelectComboItem(TaskbarStyleCombo, app.SettingsService.Settings.Taskbar.Style);

        // Show Windows 11 22H2+ compatibility notice when SWCA doesn't work on XAML taskbar
        if (TaskbarStyleService.IsModernWindows11)
            Win11TaskbarNotice.Visibility = Visibility.Visible;

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

    // ── Master / Lock ────────────────────────────────────────────────────────

    private async void LockWidgetsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.WidgetsLocked = LockWidgetsToggle.IsChecked == true;
        app.WidgetService.RefreshLockState();
        await app.SettingsService.SaveAsync();
    }

    private async void WidgetThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        if (WidgetThemeCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int theme))
        {
            app.SettingsService.Settings.Widgets.WidgetTheme = theme;
            app.WidgetService.RefreshTheme();
            await app.SettingsService.SaveAsync();
        }
    }

    // ── Clock ────────────────────────────────────────────────────────────────

    private async void ClockToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.ClockEnabled = ClockToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void ClockSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        if (ClockSizeCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out int sz))
            w.ClockFontSize = sz;
        if (ClockColorCombo.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Tag?.ToString(), out int col))
            w.ClockColorMode = col;
        if (ClockFontCombo.SelectedItem is ComboBoxItem fi && int.TryParse(fi.Tag?.ToString(), out int font))
            w.ClockFontMode = font;

        app.WidgetService.RefreshClock();
        await app.SettingsService.SaveAsync();
    }

    private async void ClockOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        w.ClockIs24Hour       = ClockFormatCheck.IsChecked == true;
        w.ClockShowDate       = ClockDateCheck.IsChecked == true;
        w.ClockShowBackground = ClockCardCheck.IsChecked == true;

        app.WidgetService.RefreshClock();
        await app.SettingsService.SaveAsync();
    }

    // ── Day Name ─────────────────────────────────────────────────────────────

    private async void DayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.DayEnabled = DayToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void DaySetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        if (DaySizeCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out int sz))
            w.DayFontSize = sz;
        if (DayColorCombo.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Tag?.ToString(), out int col))
            w.DayColorMode = col;
        if (DayFontCombo.SelectedItem is ComboBoxItem fi && int.TryParse(fi.Tag?.ToString(), out int font))
            w.DayFontMode = font;
        if (DayCaseCombo.SelectedItem is ComboBoxItem cse && int.TryParse(cse.Tag?.ToString(), out int cs))
            w.DayCaseMode = cs;
        if (DaySpacingCombo.SelectedItem is ComboBoxItem sp && int.TryParse(sp.Tag?.ToString(), out int space))
            w.DaySpacingMode = space;

        app.WidgetService.RefreshDay();
        await app.SettingsService.SaveAsync();
    }

    private async void DayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        w.DayShowBackground = DayCardCheck.IsChecked == true;

        app.WidgetService.RefreshDay();
        await app.SettingsService.SaveAsync();
    }

    // ── System Monitor (Simplicity Circles) ───────────────────────────────────

    private async void SysMonToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.SystemMonitorEnabled = SysMonToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void SysMonSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        if (SysMonSizeCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out int sz))
            w.SysMonSize = sz;
        if (SysMonColorCombo.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Tag?.ToString(), out int col))
            w.SysMonColorMode = col;
        if (SysMonLayoutCombo.SelectedItem is ComboBoxItem li && int.TryParse(li.Tag?.ToString(), out int lay))
            w.SysMonOrientation = lay;

        app.WidgetService.RefreshSysMon();
        await app.SettingsService.SaveAsync();
    }

    private async void SysMonOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        w.SysMonShowBackground = SysMonCardCheck.IsChecked == true;
        w.SysMonShowCpu        = SysMonCpuCheck.IsChecked == true;
        w.SysMonShowRam        = SysMonRamCheck.IsChecked == true;
        w.SysMonShowGpu        = SysMonGpuCheck.IsChecked == true;
        w.SysMonShowDisk       = SysMonDiskCheck.IsChecked == true;

        app.WidgetService.RefreshSysMon();
        await app.SettingsService.SaveAsync();
    }

    // ── Audio Visualizer & Media ─────────────────────────────────────────────

    private async void MediaToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Widgets.MediaEnabled = MediaToggle.IsChecked == true;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private async void MediaSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        if (VisualizerStyleCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out int st))
            w.VisualizerStyle = st;
        if (VisualizerColorCombo.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Tag?.ToString(), out int col))
            w.VisualizerColorMode = col;

        app.WidgetService.RefreshMedia();
        await app.SettingsService.SaveAsync();
    }

    private async void MediaOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        w.MediaShowBackground   = MediaCardCheck.IsChecked == true;
        w.MediaShowTrackDetails = MediaDetailsCheck.IsChecked == true;
        w.VisualizerGlow        = MediaGlowCheck.IsChecked == true;

        app.WidgetService.RefreshMedia();
        await app.SettingsService.SaveAsync();
    }

    // ── Calendar, Notes, Quote ───────────────────────────────────────────────

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

    // ── Taskbar ─────────────────────────────────────────────────────────────────────────

    private async void TaskbarStyleCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;

        if (TaskbarStyleCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int style))
        {
            app.SettingsService.Settings.Taskbar.Style = style;
            app.TaskbarStyleService.Apply((TaskbarStyleService.TaskbarStyle)style);
            await app.SettingsService.SaveAsync();
        }
    }

    private void GetTranslucentTbBtn_Click(object sender, RoutedEventArgs e)
    {
        // Open TranslucentTB in the Microsoft Store
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName   = "ms-windows-store://pdp/?ProductId=9PF4KZ2VN4W9",
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback to web if Store URI fails
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://apps.microsoft.com/detail/9pf4kz2vn4w9",
                UseShellExecute = true
            });
        }
    }
}
