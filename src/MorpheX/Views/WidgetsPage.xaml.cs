using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Application = System.Windows.Application;
using MorpheX.Core.Configuration;
using MorpheX.Services;
using Serilog;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace MorpheX;

public partial class WidgetsPage : Page
{
    private bool _isInitializing = true;

    public WidgetsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.SettingsService != null)
        {
            app.SettingsService.SettingsChanged -= OnSettingsChanged;
            app.SettingsService.SettingsChanged += OnSettingsChanged;
        }

        var w = app?.SettingsService?.Settings?.Widgets;
        if (w != null)
        {
            PopulateControlsFromSettings(w);
        }
    }

    private void PopulateControlsFromSettings(WidgetSettings w)
    {
        _isInitializing = true;
        try
        {
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
            DayCustomTextCheck.IsChecked = w.DayUseCustomText;
            DayCustomTextBox.Text        = w.DayCustomText ?? string.Empty;
            DayCustomTextPanel.Visibility = w.DayUseCustomText ? Visibility.Visible : Visibility.Collapsed;
            UpdateDayCustomCounter(w.DayCustomText?.Length ?? 0);

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
            WeatherToggle.IsChecked  = w.WeatherEnabled;
            WeatherOptionsPanel.Visibility = w.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
            SelectComboItem(WeatherUnitsCombo, w.WeatherUseFahrenheit ? 1 : 0);
            SelectComboItem(WeatherScaleCombo, w.WeatherScale);
            WeatherHiLoCheck.IsChecked = w.WeatherShowHiLo;
            WeatherHumidityCheck.IsChecked = w.WeatherShowHumidity;
            WeatherWindCheck.IsChecked = w.WeatherShowWind;
            WeatherCardBgCheck.IsChecked = w.WeatherShowBackground;
            UpdateWeatherLocationStatusText(w);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void OpenProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.NavigateTo(typeof(ProfilesPage));
        }
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

    private async void DayCustomText_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        bool on = DayCustomTextCheck.IsChecked == true;
        w.DayUseCustomText = on;
        DayCustomTextPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        app.WidgetService.RefreshDay();
        await app.SettingsService.SaveAsync();
    }

    private async void DayCustomTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        string txt = DayCustomTextBox.Text;
        // Enforce 20-char safe limit in code too
        if (txt.Length > 20) { txt = txt[..20]; DayCustomTextBox.Text = txt; DayCustomTextBox.SelectionStart = 20; }
        w.DayCustomText = txt;
        UpdateDayCustomCounter(txt.Length);

        app.WidgetService.RefreshDay();
        await app.SettingsService.SaveAsync();
    }

    private void UpdateDayCustomCounter(int length)
    {
        DayCustomTextCounter.Text = $"{length}/20";
        DayCustomTextCounter.Foreground = length >= 18
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 96, 128));
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.SettingsService != null)
        {
            app.SettingsService.SettingsChanged -= OnSettingsChanged;
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (_isInitializing) return;
            var app = (App)Application.Current;
            var w = app.SettingsService.Settings.Widgets;

            _isInitializing = true;
            try
            {
                WeatherToggle.IsChecked = w.WeatherEnabled;
                WeatherOptionsPanel.Visibility = w.WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
                SelectComboItem(WeatherUnitsCombo, w.WeatherUseFahrenheit ? 1 : 0);
                SelectComboItem(WeatherScaleCombo, w.WeatherScale);
                if (WeatherHiLoCheck != null) WeatherHiLoCheck.IsChecked = w.WeatherShowHiLo;
                if (WeatherHumidityCheck != null) WeatherHumidityCheck.IsChecked = w.WeatherShowHumidity;
                if (WeatherWindCheck != null) WeatherWindCheck.IsChecked = w.WeatherShowWind;
                if (WeatherCardBgCheck != null) WeatherCardBgCheck.IsChecked = w.WeatherShowBackground;
                UpdateWeatherLocationStatusText(w);
            }
            finally
            {
                _isInitializing = false;
            }
        });
    }

    private async void WeatherToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        bool enabled = WeatherToggle.IsChecked == true;
        app.SettingsService.Settings.Widgets.WeatherEnabled = enabled;
        WeatherOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        app.WidgetService.ApplySettings();
        await app.SettingsService.SaveAsync();
    }

    private void UpdateWeatherLocationStatusText(WidgetSettings w)
    {
        if (WeatherLocationStatusText == null) return;
        if (!string.IsNullOrEmpty(w.WeatherLocationName))
        {
            WeatherLocationStatusText.Text = $"Current: {w.WeatherLocationName} ({w.WeatherLat:F2}°, {w.WeatherLon:F2}°)";
        }
        else if (w.WeatherLat != 0 || w.WeatherLon != 0)
        {
            WeatherLocationStatusText.Text = $"Current: ({w.WeatherLat:F2}°, {w.WeatherLon:F2}°)";
        }
        else
        {
            WeatherLocationStatusText.Text = "Current: Not set (click Auto-Detect or enter a city above)";
        }
    }

    private async void WeatherOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        var w = app.SettingsService.Settings.Widgets;

        if (WeatherUnitsCombo?.SelectedItem is ComboBoxItem ui && int.TryParse(ui.Tag?.ToString(), out int u))
            w.WeatherUseFahrenheit = u == 1;

        if (WeatherScaleCombo?.SelectedItem is ComboBoxItem sc && int.TryParse(sc.Tag?.ToString(), out int s))
            w.WeatherScale = s;

        if (WeatherHiLoCheck != null)
            w.WeatherShowHiLo = WeatherHiLoCheck.IsChecked == true;

        if (WeatherHumidityCheck != null)
            w.WeatherShowHumidity = WeatherHumidityCheck.IsChecked == true;

        if (WeatherWindCheck != null)
            w.WeatherShowWind = WeatherWindCheck.IsChecked == true;

        if (WeatherCardBgCheck != null)
            w.WeatherShowBackground = WeatherCardBgCheck.IsChecked == true;

        app.WidgetService.RefreshWeather();
        await app.SettingsService.SaveAsync();
    }

    private async void WeatherSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        string city = WeatherLocationInput?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(city)) return;

        WeatherSearchBtn.IsEnabled = false;
        WeatherLocationStatusText.Text = $"Searching for '{city}'...";

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
            var json = await http.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var first = results[0];
                double lat = first.GetProperty("latitude").GetDouble();
                double lon = first.GetProperty("longitude").GetDouble();
                string name = first.GetProperty("name").GetString() ?? city;
                string? country = first.TryGetProperty("country", out var c) ? c.GetString() : null;
                string locName = !string.IsNullOrEmpty(country) ? $"{name}, {country}" : name;

                var app = (App)Application.Current;
                var w = app.SettingsService.Settings.Widgets;
                w.WeatherLat = lat;
                w.WeatherLon = lon;
                w.WeatherLocationName = locName;

                UpdateWeatherLocationStatusText(w);
                app.WidgetService.RefreshWeather();
                await app.SettingsService.SaveAsync();
                if (WeatherLocationInput != null)
                    WeatherLocationInput.Text = "";
            }
            else
            {
                WeatherLocationStatusText.Text = $"Location '{city}' not found. Please try another city name.";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to geocode weather city {City}", city);
            WeatherLocationStatusText.Text = "Geocoding failed. Check your internet connection.";
        }
        finally
        {
            WeatherSearchBtn.IsEnabled = true;
        }
    }

    private void WeatherLocationInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            WeatherSearchBtn_Click(sender, e);
        }
    }

    private async void WeatherAutoDetectBtn_Click(object sender, RoutedEventArgs e)
    {
        WeatherAutoDetectBtn.IsEnabled = false;
        WeatherLocationStatusText.Text = "Auto-detecting location via IP...";

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync("https://ipapi.co/json/");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            double lat = root.GetProperty("latitude").GetDouble();
            double lon = root.GetProperty("longitude").GetDouble();
            string city = root.GetProperty("city").GetString() ?? "";
            string country = root.TryGetProperty("country_name", out var cn) ? (cn.GetString() ?? "") : "";
            string locName = !string.IsNullOrEmpty(country) ? $"{city}, {country}" : (city.Length > 0 ? city : "Your Location");

            var app = (App)Application.Current;
            var w = app.SettingsService.Settings.Widgets;
            w.WeatherLat = lat;
            w.WeatherLon = lon;
            w.WeatherLocationName = locName;

            UpdateWeatherLocationStatusText(w);
            app.WidgetService.RefreshWeather();
            await app.SettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to auto-detect location");
            WeatherLocationStatusText.Text = "Could not auto-detect location. Please enter your city manually.";
        }
        finally
        {
            WeatherAutoDetectBtn.IsEnabled = true;
        }
    }
}
