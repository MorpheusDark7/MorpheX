using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;
using Serilog;

namespace MorpheX.Widgets;

/// <summary>
/// Transparent desktop weather widget powered by the free Open-Meteo API
/// (no API key required, no account, GDPR-compliant, non-commercial use).
/// Location can be auto-detected via IP geolocation or set manually by city name.
/// </summary>
public partial class WeatherWidget : Window
{
    // ── Constants ──────────────────────────────────────────────────────────
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";
    private const string GeoUrl  = "https://ipapi.co/json/"; // free IP geolocation
    private const int    RefreshMinutes = 30;

    // ── State ──────────────────────────────────────────────────────────────
    private readonly WidgetSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // Cached base metric values (Celsius & km/h) to allow instant unit conversion without waiting for network
    private double? _lastTempC;
    private double? _lastHiC;
    private double? _lastLoC;
    private double? _lastWindKmh;
    private int?    _lastCode;
    private int?    _lastHumidity;

    private bool   UseFahrenheit => _settings.WeatherUseFahrenheit;
    private double Latitude      => _settings.WeatherLat;
    private double Longitude     => _settings.WeatherLon;
    private string LocationName  => !string.IsNullOrEmpty(_settings.WeatherLocationName) ? _settings.WeatherLocationName : "Your Location";

    // ── Constructor ────────────────────────────────────────────────────────
    public WeatherWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Determine if initial centering on Monitor 1 is needed:
        // 1. Default initial coordinates (30, 760)
        // 2. Saved coordinates are off-screen, NaN, or invalid
        Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double estW = DesiredSize.Width > 50 ? DesiredSize.Width : 220;
        double estH = DesiredSize.Height > 50 ? DesiredSize.Height : 180;

        bool needsCenter = (settings.WeatherX == 30 && settings.WeatherY == 760)
            || !IsPositionVisibleOnAnyScreen(settings.WeatherX, settings.WeatherY, estW, estH);

        if (needsCenter)
        {
            var primary = GetPrimaryScreen();
            if (primary != null)
            {
                Left = primary.WorkingArea.Left + (primary.WorkingArea.Width - estW) / 2.0;
                Top  = primary.WorkingArea.Top + (primary.WorkingArea.Height - estH) / 2.0;
            }
            else
            {
                Left = 200;
                Top  = 200;
            }

            Loaded += (_, _) => CenterOnPrimaryMonitor();
        }
        else
        {
            Left = settings.WeatherX;
            Top  = settings.WeatherY;
        }

        UpdateLockState();
        ApplyCustomizations();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(RefreshMinutes)
        };
        _refreshTimer.Tick += async (_, _) => await FetchWeatherAsync();
        _refreshTimer.Start();

        Closing += OnClosing;

        // Initial fetch on background thread
        _ = InitLocationAndFetchAsync();
    }

    // ── Monitor centering ────────────────────────────────────────────────────
    private static System.Windows.Forms.Screen? GetPrimaryScreen()
    {
        return System.Windows.Forms.Screen.PrimaryScreen 
            ?? System.Windows.Forms.Screen.AllScreens.FirstOrDefault();
    }

    private static bool IsPositionVisibleOnAnyScreen(double x, double y, double width, double height)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            return false;
        if (x < -5000 || y < -5000 || x > 50000 || y > 50000)
            return false;

        var rect = new System.Drawing.Rectangle((int)x, (int)y, (int)Math.Max(1, width), (int)Math.Max(1, height));
        return System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect));
    }

    public void CenterOnPrimaryMonitor()
    {
        try
        {
            var primary = GetPrimaryScreen();
            if (primary == null) return;

            var wa = primary.WorkingArea; // pixels

            // WPF DIPs to pixel ratio
            double dpi = 1.0;
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpi = source.CompositionTarget.TransformToDevice.M11;
                }
            }
            catch { }

            if (dpi <= 0.1) dpi = 1.0;

            double cardWidth = ActualWidth > 50 ? ActualWidth : (DesiredSize.Width > 50 ? DesiredSize.Width : 220);
            double cardHeight = ActualHeight > 50 ? ActualHeight : (DesiredSize.Height > 50 ? DesiredSize.Height : 180);

            double waWidthDip  = wa.Width  / dpi;
            double waHeightDip = wa.Height / dpi;
            double waLeftDip   = wa.Left   / dpi;
            double waTopDip    = wa.Top    / dpi;

            Left = waLeftDip + Math.Max(0, (waWidthDip - cardWidth) / 2.0);
            Top  = waTopDip  + Math.Max(0, (waHeightDip - cardHeight) / 2.0);

            _settings.WeatherX = Left;
            _settings.WeatherY = Top;
            Save();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: Failed to center on primary monitor");
        }
    }

    // ── Location + Fetch ──────────────────────────────────────────────────
    private async Task InitLocationAndFetchAsync()
    {
        // Use saved lat/lon if available, otherwise resolve from IP
        if (_settings.WeatherLat == 0 && _settings.WeatherLon == 0)
        {
            await ResolveLocationFromIpAsync();
        }
        await FetchWeatherAsync();
    }

    private async Task ResolveLocationFromIpAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(GeoUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            double lat = root.GetProperty("latitude").GetDouble();
            double lon = root.GetProperty("longitude").GetDouble();
            string city = root.GetProperty("city").GetString() ?? "";
            string country = root.TryGetProperty("country_name", out var cn) ? (cn.GetString() ?? "") : "";
            string locName = !string.IsNullOrEmpty(country) ? $"{city}, {country}" : (city.Length > 0 ? city : "Your Location");

            _settings.WeatherLat = lat;
            _settings.WeatherLon = lon;
            _settings.WeatherLocationName = locName;
            LocationText.Text = locName;
            Save();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: IP geolocation failed");
            LocationText.Text = "📍 Click to set location";
            ConditionText.Text = "Click to set location";
        }
    }

    public async Task<bool> SetLocationByCityAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        // Check if query is raw "lat, lon"
        var parts = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
        {
            _settings.WeatherLat = lat;
            _settings.WeatherLon = lon;
            _settings.WeatherLocationName = $"{lat:F2}, {lon:F2}";
            LocationText.Text = _settings.WeatherLocationName;
            Save();
            await FetchWeatherAsync();
            return true;
        }

        try
        {
            string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=1&language=en&format=json";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var first = results[0];
                double rLat = first.GetProperty("latitude").GetDouble();
                double rLon = first.GetProperty("longitude").GetDouble();
                string name = first.GetProperty("name").GetString() ?? query;
                string? country = first.TryGetProperty("country", out var c) ? c.GetString() : null;

                _settings.WeatherLat = rLat;
                _settings.WeatherLon = rLon;
                _settings.WeatherLocationName = !string.IsNullOrEmpty(country) ? $"{name}, {country}" : name;
                LocationText.Text = _settings.WeatherLocationName;
                Save();
                await FetchWeatherAsync();
                return true;
            }
            else
            {
                ConditionText.Text = "City not found";
                ErrorText.Text = $"Could not find '{query}'";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: Geocoding failed for {Query}", query);
            ErrorText.Text = "Search failed (check internet)";
            ErrorText.Visibility = Visibility.Visible;
            return false;
        }
    }

    public async Task FetchWeatherAsync()
    {
        if (Latitude == 0 && Longitude == 0) return;

        try
        {
            string tempUnitParam = UseFahrenheit ? "fahrenheit" : "celsius";
            string windUnitParam = UseFahrenheit ? "mph" : "kmh";
            // Also request precipitation + cloud_cover to sanity-check the weather_code.
            // Global NWP models often return thunderstorm/rain codes in tropical regions
            // based on atmospheric instability (CAPE), even when no rain is occurring.
            string url = $"{BaseUrl}?latitude={Latitude:F4}&longitude={Longitude:F4}" +
                         $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m,precipitation,cloud_cover" +
                         $"&daily=temperature_2m_max,temperature_2m_min" +
                         $"&temperature_unit={tempUnitParam}" +
                         $"&wind_speed_unit={windUnitParam}&timezone=auto&forecast_days=1";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var cur   = doc.RootElement.GetProperty("current");
            var daily = doc.RootElement.GetProperty("daily");

            double temp     = cur.GetProperty("temperature_2m").GetDouble();
            int    code     = cur.GetProperty("weather_code").GetInt32();
            int    humidity = cur.GetProperty("relative_humidity_2m").GetInt32();

            // Read observed precipitation + cloud cover to correct false-positive weather codes
            double precipitation = cur.TryGetProperty("precipitation", out var precProp) && precProp.ValueKind == JsonValueKind.Number
                ? precProp.GetDouble() : 0.0;
            int cloudCover = cur.TryGetProperty("cloud_cover", out var ccProp) && ccProp.ValueKind == JsonValueKind.Number
                ? ccProp.GetInt32() : 50;

            // Correct false-positive rain/storm codes when no actual precipitation is observed
            code = SanitizeWeatherCode(code, precipitation, cloudCover);

            double? wind = null;
            if (cur.TryGetProperty("wind_speed_10m", out var wsp) && wsp.ValueKind == JsonValueKind.Number)
            {
                wind = wsp.GetDouble();
            }

            double hi = temp;
            double lo = temp;
            if (daily.TryGetProperty("temperature_2m_max", out var hArr) && hArr.GetArrayLength() > 0)
                hi = hArr[0].GetDouble();
            if (daily.TryGetProperty("temperature_2m_min", out var lArr) && lArr.GetArrayLength() > 0)
                lo = lArr[0].GetDouble();

            // Cache in base metric (Celsius & km/h) for instantaneous recalculation when toggling units
            _lastTempC   = UseFahrenheit ? (temp - 32.0) * 5.0 / 9.0 : temp;
            _lastHiC     = UseFahrenheit ? (hi - 32.0) * 5.0 / 9.0 : hi;
            _lastLoC     = UseFahrenheit ? (lo - 32.0) * 5.0 / 9.0 : lo;
            _lastWindKmh = wind.HasValue ? (UseFahrenheit ? (wind.Value * 1.60934) : wind.Value) : null;
            _lastCode    = code;
            _lastHumidity = humidity;

            await Dispatcher.InvokeAsync(() => UpdateUI(temp, code, humidity, wind, hi, lo));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: fetch failed");
            await Dispatcher.InvokeAsync(() =>
            {
                ErrorText.Text = "Could not load weather. Check internet connection.";
                ErrorText.Visibility = Visibility.Visible;
            });
        }
    }

    /// <summary>
    /// Sanity-checks a WMO weather code against observed precipitation and cloud cover.
    /// Open-Meteo's global NWP models frequently generate false-positive thunderstorm /
    /// rain codes (WMO 51-99) in tropical regions because high CAPE triggers convective
    /// parameterizations even when no precipitation actually reaches the ground.
    /// </summary>
    private static int SanitizeWeatherCode(int code, double precipMm, int cloudCoverPct)
    {
        bool isWetCode     = code is >= 51 and <= 99;
        bool hasActualRain = precipMm > 0.05; // ignore trace amounts below 0.05 mm

        if (isWetCode && !hasActualRain)
        {
            // Replace with cloud-cover-derived condition
            return cloudCoverPct switch
            {
                <= 10 => 0,  // Clear sky
                <= 30 => 1,  // Mainly clear
                <= 65 => 2,  // Partly cloudy
                _     => 3   // Overcast
            };
        }

        return code;
    }

    private void RenderCurrentWeather()
    {
        if (!_lastTempC.HasValue) return;

        double dispTemp = UseFahrenheit ? (_lastTempC.Value * 9.0 / 5.0 + 32.0) : _lastTempC.Value;
        double dispHi   = _lastHiC.HasValue ? (UseFahrenheit ? (_lastHiC.Value * 9.0 / 5.0 + 32.0) : _lastHiC.Value) : dispTemp;
        double dispLo   = _lastLoC.HasValue ? (UseFahrenheit ? (_lastLoC.Value * 9.0 / 5.0 + 32.0) : _lastLoC.Value) : dispTemp;
        double? dispWind = _lastWindKmh.HasValue ? (UseFahrenheit ? (_lastWindKmh.Value * 0.621371) : _lastWindKmh.Value) : null;

        UpdateUI(dispTemp, _lastCode ?? 0, _lastHumidity ?? 0, dispWind, dispHi, dispLo);
    }

    private void UpdateUI(double temp, int code, int humidity, double? wind, double hi, double lo)
    {
        string sym = UseFahrenheit ? "°F" : "°C";
        TempText.Text      = $"{Math.Round(temp, 1)}{sym}";
        HiLoText.Text      = $"↑ {Math.Round(hi, 0)}{sym}  ↓ {Math.Round(lo, 0)}{sym}";
        HumidityText.Text  = $"💧 {humidity}%";

        // Display wind speed only if it exists in API response and is enabled in settings
        if (wind.HasValue && wind.Value >= 0 && _settings.WeatherShowWind)
        {
            string wUnit = UseFahrenheit ? "mph" : "km/h";
            WindText.Text = $"💨 {Math.Round(wind.Value, 0)} {wUnit}";
            WindText.Visibility = Visibility.Visible;
        }
        else
        {
            WindText.Visibility = Visibility.Collapsed;
        }

        ConditionText.Text = WmoCodeToDescription(code);
        WeatherEmojiText.Text = WmoCodeToEmoji(code);
        LocationText.Text  = LocationName;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // ── WMO Weather Code mapping ───────────────────────────────────────────
    private static string WmoCodeToEmoji(int code) => code switch
    {
        0                   => "☀️",
        1                   => "🌤",
        2                   => "⛅",
        3                   => "☁️",
        45 or 48            => "🌫",
        51 or 53 or 55      => "🌦",
        61 or 63 or 65      => "🌧",
        66 or 67            => "🌨",
        71 or 73 or 75 or 77 => "❄️",
        80 or 81 or 82      => "🌦",
        85 or 86            => "🌨",
        95                  => "⛈",
        96 or 99            => "⛈",
        _                   => "🌡"
    };

    private static string WmoCodeToDescription(int code) => code switch
    {
        0                   => "Clear sky",
        1                   => "Mainly clear",
        2                   => "Partly cloudy",
        3                   => "Overcast",
        45                  => "Foggy",
        48                  => "Icy fog",
        51                  => "Light drizzle",
        53                  => "Drizzle",
        55                  => "Dense drizzle",
        61                  => "Light rain",
        63                  => "Moderate rain",
        65                  => "Heavy rain",
        66                  => "Light freezing rain",
        67                  => "Heavy freezing rain",
        71                  => "Light snow",
        73                  => "Moderate snow",
        75                  => "Heavy snow",
        77                  => "Snow grains",
        80                  => "Light showers",
        81                  => "Showers",
        82                  => "Heavy showers",
        85                  => "Snow showers",
        86                  => "Heavy snow showers",
        95                  => "Thunderstorm",
        96                  => "Thunderstorm + hail",
        99                  => "Thunderstorm + heavy hail",
        _                   => "Unknown conditions"
    };

    // ── Input & Context Menu ───────────────────────────────────────────────
    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private async void RefreshNow_Click(object sender, RoutedEventArgs e)
    {
        await FetchWeatherAsync();
    }

    private async void LocationText_Click(object sender, MouseButtonEventArgs e)
    {
        await PromptSetLocationAsync();
    }

    private async void SetLocation_Click(object sender, RoutedEventArgs e)
    {
        await PromptSetLocationAsync();
    }

    private async Task PromptSetLocationAsync()
    {
        string current = _settings.WeatherLocationName.Length > 0 ? _settings.WeatherLocationName : "";
        string? result = Views.ModernDialogWindow.Prompt(
            "Weather Location",
            "Set Your City or Location",
            "Enter your city name (e.g. New York, London, Tokyo, Mumbai):",
            current,
            "Save",
            "Cancel",
            owner: this);

        if (!string.IsNullOrWhiteSpace(result))
        {
            ConditionText.Text = "Searching...";
            bool ok = await SetLocationByCityAsync(result.Trim());
            if (!ok)
            {
                Views.ModernDialogWindow.Warning(
                    "Location Not Found",
                    $"Could not find '{result}'",
                    "Please check the spelling or enter 'Latitude, Longitude' (e.g. 51.5, -0.12).");
            }
        }
    }

    private async void AutoDetectLocation_Click(object sender, RoutedEventArgs e)
    {
        ConditionText.Text = "Detecting location...";
        await ResolveLocationFromIpAsync();
        await FetchWeatherAsync();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetTheme = _settings.WidgetTheme == 0 ? 1 : 0;
        ApplyTheme();
        Save();
        ((App)Application.Current).WidgetService.RefreshTheme();
    }

    private async void ToggleUnits_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherUseFahrenheit = !_settings.WeatherUseFahrenheit;
        ApplyCustomizations(); // Instant display recalculation
        Save();                // Sync with Settings & WidgetsPage
        await FetchWeatherAsync();
    }

    private void ToggleCardBg_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherShowBackground = !_settings.WeatherShowBackground;
        ApplyCustomizations();
        Save();
    }

    private void ToggleHiLo_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherShowHiLo = !_settings.WeatherShowHiLo;
        ApplyCustomizations();
        Save();
    }

    private void ToggleHumidity_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherShowHumidity = !_settings.WeatherShowHumidity;
        ApplyCustomizations();
        Save();
    }

    private void ToggleWind_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherShowWind = !_settings.WeatherShowWind;
        ApplyCustomizations();
        Save();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        Save();
        ((App)Application.Current).WidgetService.RefreshLockState();
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherEnabled = false;
        Save();
        Close();
    }

    // ── Appearance & Customization ─────────────────────────────────────────
    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    public void ApplyCustomizations()
    {
        // 1. Scale
        double scale = _settings.WeatherScale switch
        {
            0 => 0.85,
            2 => 1.20,
            _ => 1.0
        };
        WidgetScaleTransform.ScaleX = scale;
        WidgetScaleTransform.ScaleY = scale;

        // 2. Details visibility
        HiLoText.Visibility = _settings.WeatherShowHiLo ? Visibility.Visible : Visibility.Collapsed;
        HumidityText.Visibility = _settings.WeatherShowHumidity ? Visibility.Visible : Visibility.Collapsed;
        WindText.Visibility = (_settings.WeatherShowWind && _lastWindKmh.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        HiLoHumidityRow.Visibility = (_settings.WeatherShowHiLo || _settings.WeatherShowHumidity) ? Visibility.Visible : Visibility.Collapsed;

        // 3. Theme & Card styling
        ApplyTheme();

        // 4. Update Context Menu checkboxes & headers
        if (HiLoMenuItem != null) HiLoMenuItem.IsChecked = _settings.WeatherShowHiLo;
        if (HumidityMenuItem != null) HumidityMenuItem.IsChecked = _settings.WeatherShowHumidity;
        if (WindMenuItem != null) WindMenuItem.IsChecked = _settings.WeatherShowWind;
        if (UnitsMenuItem != null) UnitsMenuItem.Header = UseFahrenheit ? "Switch to °C" : "Switch to °F";
        LocationText.Text = LocationName;

        // 5. Instantly recalculate & render displayed numbers with current unit
        RenderCurrentWeather();
    }

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        bool showBg  = _settings.WeatherShowBackground;

        if (showBg)
        {
            WidgetStyles.ApplyFrostedGlass(CardBorder, showBackground: true, cornerRadius: 14,
                padding: new Thickness(16, 14, 16, 14), isLightTheme: isLight);
        }
        else
        {
            CardBorder.Background  = System.Windows.Media.Brushes.Transparent;
            CardBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            CardBorder.Padding     = new Thickness(16, 14, 16, 14);
        }

        ThemeMenuItem.Header = isLight ? "Dark Theme" : "Light Theme";
        if (CardBgMenuItem != null)
            CardBgMenuItem.Header = showBg ? "Disable Frosted Card" : "Enable Frosted Card";

        // Adjust text colours for light / dark theme
        var primary   = isLight ? "#CC000000" : "#FFFFFFFF";
        var secondary = isLight ? "#88000000" : "#88FFFFFF";
        var tertiary  = isLight ? "#66000000" : "#66FFFFFF";
        var brushConv = new System.Windows.Media.BrushConverter();

        TempText.Foreground      = brushConv.ConvertFromString(primary)   as System.Windows.Media.Brush;
        ConditionText.Foreground = brushConv.ConvertFromString(secondary) as System.Windows.Media.Brush;
        HiLoText.Foreground      = brushConv.ConvertFromString(secondary) as System.Windows.Media.Brush;
        HumidityText.Foreground  = brushConv.ConvertFromString(secondary) as System.Windows.Media.Brush;
        WindText.Foreground      = brushConv.ConvertFromString(tertiary)  as System.Windows.Media.Brush;
        LocationText.Foreground  = brushConv.ConvertFromString(secondary) as System.Windows.Media.Brush;
    }

    // ── Position save ────────────────────────────────────────────────────
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.WeatherX = Left;
        _settings.WeatherY = Top;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _settings.WeatherX = Left;
        _settings.WeatherY = Top;
        Save();
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
