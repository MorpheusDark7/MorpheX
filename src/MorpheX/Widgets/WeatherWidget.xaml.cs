using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;
using Serilog;
using Brush     = System.Windows.Media.Brush;
using Brushes   = System.Windows.Media.Brushes;
using Color     = System.Windows.Media.Color;
using Canvas    = System.Windows.Controls.Canvas;
using Rectangle = System.Windows.Shapes.Rectangle;
using WpfColor  = System.Windows.Media.Color;
using WpfCanvas = System.Windows.Controls.Canvas;
using WpfRect   = System.Windows.Shapes.Rectangle;

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

            // Settings fields are not UI elements — safe to write from any thread
            _settings.WeatherLat = lat;
            _settings.WeatherLon = lon;
            _settings.WeatherLocationName = locName;
            Save();

            // UI element updates must be marshaled back to the UI thread
            await Dispatcher.InvokeAsync(() => { LocationText.Text = locName; });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: IP geolocation failed");
            await Dispatcher.InvokeAsync(() =>
            {
                LocationText.Text = "📍 Click to set location";
                ConditionText.Text = "Click to set location";
            });
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
        DrawWeatherIcon(code);
        ApplyConditionTint(code);
        LocationText.Text  = LocationName;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // ── WMO Weather Code mapping ───────────────────────────────────────────
    // (Emoji lookup removed — replaced by WPF vector DrawWeatherIcon())

    private static string WmoCodeToDescription(int code) => code switch
    {
        0                   => "Clear sky",
        1                   => "Mainly clear",
        2                   => "Partly cloudy",
        3                   => "Cloudy",
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
        Save();
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
    public void Reposition()
    {
        Left = _settings.WeatherX;
        Top = _settings.WeatherY;
        ApplyCustomizations();
        _ = FetchWeatherAsync();
        UpdateLockState();
    }

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
            // Apply frosted border/shadow but preserve our dynamic gradient background
            CardBorder.BorderBrush = new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255));
            CardBorder.BorderThickness = new Thickness(1);
            CardBorder.Padding = new Thickness(16, 14, 16, 14);
            // Background gradient is managed by ApplyConditionTint — don't override it here
        }
        else
        {
            CardBorder.Background  = Brushes.Transparent;
            CardBorder.BorderBrush = Brushes.Transparent;
            CardBorder.Padding     = new Thickness(16, 14, 16, 14);
        }

        if (CardBgMenuItem != null)
            CardBgMenuItem.Header = showBg ? "Disable Frosted Card" : "Enable Frosted Card";

        // Adjust text colours for light / dark theme
        var primary   = isLight ? "#CC000000" : "#FFFFFFFF";
        var secondary = isLight ? "#99000000" : "#DDFFFFFF";
        var tertiary  = isLight ? "#88000000" : "#B0FFFFFF";
        var brushConv = new BrushConverter();

        TempText.Foreground      = brushConv.ConvertFromString(primary)   as Brush;
        ConditionText.Foreground = brushConv.ConvertFromString(secondary) as Brush;
        HiLoText.Foreground      = brushConv.ConvertFromString(secondary) as Brush;
        HumidityText.Foreground  = brushConv.ConvertFromString(secondary) as Brush;
        WindText.Foreground      = brushConv.ConvertFromString(tertiary)  as Brush;
        LocationText.Foreground  = brushConv.ConvertFromString(secondary) as Brush;
    }

    // ── Dynamic condition tint ─────────────────────────────────────────────
    /// <summary>
    /// Shifts the card background gradient to match the current weather condition,
    /// similar to how Google Weather changes its background colour dynamically.
    /// Only takes effect when the frosted card background is enabled.
    /// </summary>
    private void ApplyConditionTint(int code)
    {
        if (!_settings.WeatherShowBackground) return;

        bool isLight = _settings.WidgetTheme == 1;

        // Pick gradient colours based on condition group and theme
        (Color c1, Color c2) = (isLight, code) switch
        {
            (true, 0 or 1) => (Color.FromArgb(0xDD, 0xFF, 0xF3, 0xD6), Color.FromArgb(0xDD, 0xFD, 0xE6, 0xAA)),
            (true, 2)      => (Color.FromArgb(0xDD, 0xEE, 0xF2, 0xF8), Color.FromArgb(0xDD, 0xDA, 0xE4, 0xF2)),
            (true, 3)      => (Color.FromArgb(0xDD, 0xEA, 0xED, 0xF1), Color.FromArgb(0xDD, 0xD8, 0xDC, 0xE4)),
            (true, 45 or 48) => (Color.FromArgb(0xDD, 0xE4, 0xE8, 0xEE), Color.FromArgb(0xDD, 0xD0, 0xD6, 0xE0)),
            (true, 51 or 53 or 55) => (Color.FromArgb(0xDD, 0xDE, 0xEC, 0xF9), Color.FromArgb(0xDD, 0xC4, 0xDC, 0xF5)),
            (true, 61 or 63 or 65 or 80 or 81 or 82) => (Color.FromArgb(0xDD, 0xCC, 0xDF, 0xF5), Color.FromArgb(0xDD, 0xAE, 0xCD, 0xEE)),
            (true, 66 or 67) => (Color.FromArgb(0xDD, 0xDA, 0xEB, 0xF7), Color.FromArgb(0xDD, 0xC0, 0xDC, 0xF0)),
            (true, 71 or 73 or 75 or 77 or 85 or 86) => (Color.FromArgb(0xDD, 0xE8, 0xF2, 0xFA), Color.FromArgb(0xDD, 0xD4, 0xE4, 0xF2)),
            (true, 95 or 96 or 99) => (Color.FromArgb(0xDD, 0xE4, 0xDE, 0xF5), Color.FromArgb(0xDD, 0xCE, 0xC2, 0xEB)),
            (true, _)      => (Color.FromArgb(0xDD, 0xF0, 0xF2, 0xF6), Color.FromArgb(0xDD, 0xE0, 0xE4, 0xEC)),

            (false, 0 or 1) => (Color.FromArgb(0xCC, 0x1A, 0x14, 0x08), Color.FromArgb(0xCC, 0x2A, 0x1E, 0x06)),
            (false, 2)      => (Color.FromArgb(0xCC, 0x10, 0x14, 0x1E), Color.FromArgb(0xCC, 0x18, 0x1E, 0x2A)),
            (false, 3)      => (Color.FromArgb(0xCC, 0x0E, 0x0E, 0x12), Color.FromArgb(0xCC, 0x16, 0x16, 0x1C)),
            (false, 45 or 48) => (Color.FromArgb(0xCC, 0x14, 0x14, 0x18), Color.FromArgb(0xCC, 0x1E, 0x1E, 0x24)),
            (false, 51 or 53 or 55) => (Color.FromArgb(0xCC, 0x08, 0x12, 0x22), Color.FromArgb(0xCC, 0x0C, 0x18, 0x2C)),
            (false, 61 or 63 or 65 or 80 or 81 or 82) => (Color.FromArgb(0xCC, 0x06, 0x0E, 0x1E), Color.FromArgb(0xCC, 0x08, 0x14, 0x28)),
            (false, 66 or 67) => (Color.FromArgb(0xCC, 0x08, 0x14, 0x26), Color.FromArgb(0xCC, 0x0A, 0x1A, 0x30)),
            (false, 71 or 73 or 75 or 77 or 85 or 86) => (Color.FromArgb(0xCC, 0x0C, 0x16, 0x28), Color.FromArgb(0xCC, 0x14, 0x22, 0x38)),
            (false, 95 or 96 or 99) => (Color.FromArgb(0xCC, 0x0E, 0x08, 0x1E), Color.FromArgb(0xCC, 0x18, 0x0C, 0x2C)),
            _               => (Color.FromArgb(0xCC, 0x10, 0x10, 0x14), Color.FromArgb(0xCC, 0x18, 0x18, 0x22))
        };

        CardBorder.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(c1, 0.0),
                new GradientStop(c2, 1.0)
            },
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 1));
    }

    // ── WPF Vector weather icon drawing ───────────────────────────────────
    /// <summary>
    /// Draws a colourful WPF vector icon into the WeatherIconCanvas based on the
    /// WMO weather code. All shapes are drawn programmatically — no images or emoji.
    /// Canvas coordinate space: 64×64.
    /// </summary>
    private void DrawWeatherIcon(int code)
    {
        WeatherIconCanvas.Children.Clear();

        if      (code == 0)                                 DrawSun(fullyVisible: true);
        else if (code == 1)                                 DrawSunWithCloud(smallCloud: true);
        else if (code == 2)                                 DrawSunWithCloud(smallCloud: false);
        else if (code == 3)                                 DrawCloud(color: WpfColor.FromRgb(0xC0, 0xC8, 0xD8));
        else if (code is 45 or 48)                          DrawFog();
        else if (code is 51 or 53 or 55)                    DrawRain(drops: 2, heavy: false);
        else if (code is 61 or 63 or 80 or 81)              DrawRain(drops: 3, heavy: false);
        else if (code is 65 or 82)                          DrawRain(drops: 4, heavy: true);
        else if (code is 66 or 67)                          DrawSleet();
        else if (code is 71 or 73 or 77 or 85)              DrawSnow(flakes: 3);
        else if (code is 75 or 86)                          DrawSnow(flakes: 5);
        else if (code is 95 or 96 or 99)                    DrawThunderstorm();
        else                                                DrawSun(fullyVisible: true); // fallback
    }

    private void DrawSun(bool fullyVisible)
    {
        var c = WeatherIconCanvas;
        // Glow halo
        var halo = new Ellipse { Width = 46, Height = 46 };
        halo.Fill = new RadialGradientBrush(
            WpfColor.FromArgb(80, 0xFF, 0xDD, 0x44),
            WpfColor.FromArgb(0, 0xFF, 0xDD, 0x44));
        WpfCanvas.SetLeft(halo, 9); WpfCanvas.SetTop(halo, 9);
        c.Children.Add(halo);

        // Sun rays
        var rayBrush = new SolidColorBrush(WpfColor.FromArgb(200, 0xFF, 0xCC, 0x33));
        double cx = 32, cy = 32, r = 13, rayLen = 7;
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            double x1 = cx + r * Math.Cos(angle);
            double y1 = cy + r * Math.Sin(angle);
            double x2 = cx + (r + rayLen) * Math.Cos(angle);
            double y2 = cy + (r + rayLen) * Math.Sin(angle);
            c.Children.Add(new Line { X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=rayBrush, StrokeThickness=2.5, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round });
        }

        // Sun disc
        var disc = new Ellipse { Width = 26, Height = 26 };
        disc.Fill = new RadialGradientBrush(WpfColor.FromRgb(0xFF, 0xEE, 0x44), WpfColor.FromRgb(0xFF, 0xAA, 0x11));
        WpfCanvas.SetLeft(disc, 19); WpfCanvas.SetTop(disc, 19);
        c.Children.Add(disc);
    }

    private void DrawSunWithCloud(bool smallCloud)
    {
        var c = WeatherIconCanvas;
        // Partial sun (top-left)
        var rayBrush = new SolidColorBrush(WpfColor.FromArgb(180, 0xFF, 0xCC, 0x33));
        double cx = 22, cy = 22, r = 10, rayLen = 5;
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            double x1 = cx + r * Math.Cos(angle);
            double y1 = cy + r * Math.Sin(angle);
            double x2 = cx + (r + rayLen) * Math.Cos(angle);
            double y2 = cy + (r + rayLen) * Math.Sin(angle);
            c.Children.Add(new Line { X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=rayBrush, StrokeThickness=2, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round });
        }
        var disc = new Ellipse { Width = 20, Height = 20 };
        disc.Fill = new RadialGradientBrush(WpfColor.FromRgb(0xFF, 0xEE, 0x44), WpfColor.FromRgb(0xFF, 0xAA, 0x11));
        WpfCanvas.SetLeft(disc, 12); WpfCanvas.SetTop(disc, 12);
        c.Children.Add(disc);

        // Cloud in front (bottom-right)
        DrawCloudAt(smallCloud ? 28 : 24, smallCloud ? 34 : 30,
                    smallCloud ? 32 : 36, smallCloud ? 24 : 28,
                    WpfColor.FromRgb(0xD4, 0xDC, 0xEC));
    }

    private void DrawCloud(WpfColor color)
    {
        DrawCloudAt(10, 24, 44, 28, color);
    }

    private void DrawCloudAt(double left, double top, double width, double height, WpfColor color)
    {
        var c = WeatherIconCanvas;
        var brush = new LinearGradientBrush(
            WpfColor.FromArgb(255, (byte)Math.Min(color.R + 30, 255), (byte)Math.Min(color.G + 30, 255), (byte)Math.Min(color.B + 30, 255)),
            color, 90);

        // Cloud body: two overlapping ellipses + a rectangle base
        double r1 = height * 0.55, r2 = height * 0.40;
        var e1 = new Ellipse { Width = r1 * 2, Height = r1 * 2, Fill = brush };
        WpfCanvas.SetLeft(e1, left + width * 0.20 - r1); WpfCanvas.SetTop(e1, top - r1 * 0.6);
        c.Children.Add(e1);

        var e2 = new Ellipse { Width = r2 * 2, Height = r2 * 2, Fill = brush };
        WpfCanvas.SetLeft(e2, left + width * 0.55 - r2); WpfCanvas.SetTop(e2, top - r2 * 0.2);
        c.Children.Add(e2);

        var body = new WpfRect { Width = width, Height = height * 0.55, Fill = brush };
        body.RadiusX = height * 0.27; body.RadiusY = height * 0.27;
        WpfCanvas.SetLeft(body, left); WpfCanvas.SetTop(body, top + height * 0.20);
        c.Children.Add(body);
    }

    private void DrawFog()
    {
        var c = WeatherIconCanvas;
        var fogBrush = new SolidColorBrush(WpfColor.FromArgb(160, 0xC0, 0xC8, 0xD4));
        for (int i = 0; i < 4; i++)
        {
            double y = 18 + i * 10;
            double xOff = (i % 2 == 0) ? 0 : 4;
            var line = new Line { X1 = 8 + xOff, Y1 = y, X2 = 56 - xOff, Y2 = y,
                Stroke = fogBrush, StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            c.Children.Add(line);
        }
    }

    private void DrawRain(int drops, bool heavy)
    {
        var c = WeatherIconCanvas;
        // Cloud
        DrawCloudAt(10, 8, 44, 30, WpfColor.FromRgb(0xA0, 0xAC, 0xC0));
        // Rain drops
        var dropBrush = new LinearGradientBrush(
            WpfColor.FromArgb(220, 0x60, 0xA8, 0xFF),
            WpfColor.FromArgb(140, 0x30, 0x78, 0xCC), 90);
        double[] xPositions = drops switch { 2 => new[]{20.0,40.0}, 3 => new[]{16.0,32.0,48.0}, _ => new[]{12.0,24.0,38.0,50.0} };
        for (int i = 0; i < drops; i++)
        {
            double x = xPositions[Math.Min(i, xPositions.Length-1)];
            double h = heavy ? 10 : 7;
            var drop = new Line { X1=x+3, Y1=42, X2=x, Y2=42+h,
                Stroke=dropBrush, StrokeThickness=2.5,
                StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round };
            c.Children.Add(drop);
        }
    }

    private void DrawSleet()
    {
        var c = WeatherIconCanvas;
        DrawCloudAt(10, 8, 44, 28, WpfColor.FromRgb(0x98, 0xA8, 0xC0));
        var rain = new SolidColorBrush(WpfColor.FromArgb(200, 0x60, 0xA8, 0xFF));
        var snow = new SolidColorBrush(WpfColor.FromArgb(220, 0xC8, 0xE8, 0xFF));
        // 2 rain lines
        c.Children.Add(new Line {X1=16,Y1=42,X2=13,Y2=50,Stroke=rain,StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        c.Children.Add(new Line {X1=32,Y1=42,X2=29,Y2=50,Stroke=rain,StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        // 1 snowflake dot
        var dot = new Ellipse { Width=5, Height=5, Fill=snow };
        WpfCanvas.SetLeft(dot, 44); WpfCanvas.SetTop(dot, 46);
        c.Children.Add(dot);
    }

    private void DrawSnow(int flakes)
    {
        var c = WeatherIconCanvas;
        DrawCloudAt(10, 6, 44, 28, WpfColor.FromRgb(0xB8, 0xCC, 0xE4));
        var snowBrush = new SolidColorBrush(WpfColor.FromArgb(230, 0xCC, 0xE8, 0xFF));
        double[] xs = { 15, 25, 35, 45, 20 };
        double[] ys = { 44, 50, 44, 50, 56 };
        for (int i = 0; i < Math.Min(flakes, xs.Length); i++)
        {
            // Snowflake as a small 6-pointed star (2 lines + 1 diagonal cross)
            double x = xs[i], y = ys[i], r = 4;
            c.Children.Add(new Line {X1=x,Y1=y-r,X2=x,Y2=y+r,Stroke=snowBrush,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            c.Children.Add(new Line {X1=x-r*0.87,Y1=y-r*0.5,X2=x+r*0.87,Y2=y+r*0.5,Stroke=snowBrush,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            c.Children.Add(new Line {X1=x+r*0.87,Y1=y-r*0.5,X2=x-r*0.87,Y2=y+r*0.5,Stroke=snowBrush,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        }
    }

    private void DrawThunderstorm()
    {
        var c = WeatherIconCanvas;
        DrawCloudAt(8, 6, 48, 30, WpfColor.FromRgb(0x60, 0x60, 0x80));
        // Lightning bolt
        var boltBrush = new LinearGradientBrush(
            WpfColor.FromArgb(255, 0xFF, 0xEE, 0x44),
            WpfColor.FromArgb(200, 0xFF, 0xAA, 0x00), 90);
        var bolt = new Polygon
        {
            Fill = boltBrush,
            Points = new PointCollection
            {
                new System.Windows.Point(34, 38),
                new System.Windows.Point(27, 52),
                new System.Windows.Point(33, 52),
                new System.Windows.Point(28, 63),
                new System.Windows.Point(40, 47),
                new System.Windows.Point(33, 47)
            },
            Effect = new DropShadowEffect { BlurRadius=8, ShadowDepth=0, Color=WpfColor.FromRgb(0xFF,0xEE,0x00), Opacity=0.8 }
        };
        c.Children.Add(bolt);
        // Rain drops behind the bolt
        var dropBrush = new SolidColorBrush(WpfColor.FromArgb(180, 0x60, 0xA8, 0xFF));
        c.Children.Add(new Line {X1=16,Y1=40,X2=13,Y2=50,Stroke=dropBrush,StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        c.Children.Add(new Line {X1=22,Y1=42,X2=19,Y2=52,Stroke=dropBrush,StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
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
