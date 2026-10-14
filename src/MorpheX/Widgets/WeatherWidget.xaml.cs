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
/// Location is resolved from the Windows locale/timezone; a lat/lon can be
/// stored in settings for precision.
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

    private double _lat;
    private double _lon;
    private string _locationName = "Your Location";
    private bool   _useFahrenheit;

    // ── Constructor ────────────────────────────────────────────────────────
    public WeatherWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        _useFahrenheit = settings.WeatherUseFahrenheit;

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
        ApplyTheme();

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
        if (_settings.WeatherLat != 0 || _settings.WeatherLon != 0)
        {
            _lat = _settings.WeatherLat;
            _lon = _settings.WeatherLon;
            _locationName = _settings.WeatherLocationName.Length > 0
                ? _settings.WeatherLocationName
                : "Custom Location";
        }
        else
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
            _lat = root.GetProperty("latitude").GetDouble();
            _lon = root.GetProperty("longitude").GetDouble();
            _locationName = root.GetProperty("city").GetString() ?? "Your Location";

            _settings.WeatherLat = _lat;
            _settings.WeatherLon = _lon;
            _settings.WeatherLocationName = _locationName;
            _ = ((App)Application.Current).SettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WeatherWidget: IP geolocation failed, using default (London)");
            _lat = 51.5074;
            _lon = -0.1278;
            _locationName = "London";
        }
    }

    private async Task FetchWeatherAsync()
    {
        try
        {
            string unit = _useFahrenheit ? "fahrenheit" : "celsius";
            string url = $"{BaseUrl}?latitude={_lat:F4}&longitude={_lon:F4}" +
                         $"&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m" +
                         $"&daily=temperature_2m_max,temperature_2m_min" +
                         $"&temperature_unit={unit}" +
                         $"&wind_speed_unit=kmh&timezone=auto&forecast_days=1";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var cur   = doc.RootElement.GetProperty("current");
            var daily = doc.RootElement.GetProperty("daily");

            double temp     = cur.GetProperty("temperature_2m").GetDouble();
            int    code     = cur.GetProperty("weather_code").GetInt32();
            int    humidity = cur.GetProperty("relative_humidity_2m").GetInt32();
            double wind     = cur.GetProperty("wind_speed_10m").GetDouble();
            double hi       = daily.GetProperty("temperature_2m_max")[0].GetDouble();
            double lo       = daily.GetProperty("temperature_2m_min")[0].GetDouble();

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

    private void UpdateUI(double temp, int code, int humidity, double wind, double hi, double lo)
    {
        string sym = _useFahrenheit ? "°F" : "°C";
        TempText.Text      = $"{Math.Round(temp, 1)}{sym}";
        HiLoText.Text      = $"↑ {Math.Round(hi, 0)}{sym}  ↓ {Math.Round(lo, 0)}{sym}";
        HumidityText.Text  = $"💧 {humidity}%";
        WindText.Text      = $"💨 {Math.Round(wind, 0)} km/h";
        ConditionText.Text = WmoCodeToDescription(code);
        WeatherEmojiText.Text = WmoCodeToEmoji(code);
        LocationText.Text  = _locationName;
        LastUpdatedText.Text = DateTime.Now.ToString("HH:mm");
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // ── WMO Weather Code mapping ───────────────────────────────────────────
    // Source: https://open-meteo.com/en/docs (WMO Weather interpretation codes)
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

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetTheme = _settings.WidgetTheme == 0 ? 1 : 0;
        ApplyTheme();
        Save();
        ((App)Application.Current).WidgetService.RefreshTheme();
    }

    private async void ToggleUnits_Click(object sender, RoutedEventArgs e)
    {
        _useFahrenheit = !_useFahrenheit;
        _settings.WeatherUseFahrenheit = _useFahrenheit;
        UnitsMenuItem.Header = _useFahrenheit ? "Switch to °C" : "Switch to °F";
        Save();
        await FetchWeatherAsync();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        Save();
        ((App)Application.Current).WidgetService.RefreshLockState();
    }

    private void CenterOnScreen_Click(object sender, RoutedEventArgs e)
    {
        CenterOnPrimaryMonitor();
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.WeatherEnabled = false;
        Save();
        Close();
    }

    // ── Appearance ────────────────────────────────────────────────────────
    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, showBackground: true, cornerRadius: 14,
            padding: new Thickness(16, 14, 16, 14), isLightTheme: isLight);
        ThemeMenuItem.Header = isLight ? "Dark Theme" : "Light Theme";

        // Adjust text colours for light theme
        var primary   = isLight ? "#CC000000" : "#FFFFFFFF";
        var secondary = isLight ? "#88000000" : "#88FFFFFF";
        TempText.Foreground      = new System.Windows.Media.BrushConverter().ConvertFromString(primary)   as System.Windows.Media.Brush;
        ConditionText.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(secondary) as System.Windows.Media.Brush;
        HiLoText.Foreground      = new System.Windows.Media.BrushConverter().ConvertFromString(secondary) as System.Windows.Media.Brush;
        HumidityText.Foreground  = new System.Windows.Media.BrushConverter().ConvertFromString(secondary) as System.Windows.Media.Brush;
        WindText.Foreground      = new System.Windows.Media.BrushConverter().ConvertFromString(secondary) as System.Windows.Media.Brush;
        LocationText.Foreground  = new System.Windows.Media.BrushConverter().ConvertFromString(secondary) as System.Windows.Media.Brush;
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
