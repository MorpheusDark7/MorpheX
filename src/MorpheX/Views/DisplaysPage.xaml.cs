using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using MorpheX.Core.Configuration;
using Serilog;

namespace MorpheX;

public sealed class DisplayViewModel
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Visibility PrimaryVisibility { get; set; } = Visibility.Collapsed;
    public string SpecsString { get; set; } = string.Empty;
    public string ActiveWallpaperString { get; set; } = string.Empty;

    public bool HasAudioControls { get; set; }
    public bool IsMuted { get; set; }
    public bool IsNotMuted => !IsMuted;
    public int Volume { get; set; }
    public string VolumeText => $"{Volume}%";
    public string MuteTooltip => IsMuted ? "Unmute this display" : "Mute this display";

    // Per-monitor effects (0-200 range for sliders, maps to 0.0-2.0f)
    public int BrightnessValue { get; set; } = 100;
    public int ContrastValue { get; set; } = 100;
    public int SaturationValue { get; set; } = 100;
    public int ColorTempValue { get; set; } = 0;
    public string BrightnessText => $"{BrightnessValue}%";
    public string ContrastText => $"{ContrastValue}%";
    public string SaturationText => $"{SaturationValue}%";
    public string ColorTempText => ColorTempValue == 0 ? "Neutral" : (ColorTempValue > 0 ? $"+{ColorTempValue} Warm" : $"{ColorTempValue} Cool");
    public bool HasEffectsOverride { get; set; }
}

public partial class DisplaysPage : Page
{
    // Guard against recursive slider-change events during load
    private bool _loadingEffects;

    // Tracks which monitors have already shown the OLED brightness warning this session.
    // Clears for a monitor when brightness goes back below 170%, so warning fires again
    // if they exceed it a second time.
    private readonly HashSet<string> _oledWarned = new();

    public DisplaysPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        UpdatePlacementModeUI(app.SettingsService.Settings.Display.PlacementMode);
        UpdateScalingModeUI(app.SettingsService.Settings.Display.DefaultScalingMode);
        RefreshDisplays();
    }

    private void UpdateScalingModeUI(ScalingMode mode)
    {
        var accentBrush = (System.Windows.Media.Brush)FindResource("MxAccentBrush");
        var subtleAccentBrush = (System.Windows.Media.Brush)FindResource("MxAccentSubtleBrush");
        var defaultBorderBrush = (System.Windows.Media.Brush)FindResource("CardStrokeColorDefaultBrush");
        var defaultBgBrush = (System.Windows.Media.Brush)FindResource("CardBackgroundFillColorDefaultBrush");

        ScalingCropCard.BorderBrush = defaultBorderBrush;
        ScalingCropCard.Background = defaultBgBrush;
        ScalingFitCard.BorderBrush = defaultBorderBrush;
        ScalingFitCard.Background = defaultBgBrush;
        ScalingStretchCard.BorderBrush = defaultBorderBrush;
        ScalingStretchCard.Background = defaultBgBrush;

        switch (mode)
        {
            case ScalingMode.Fit:
                ScalingFitCard.BorderBrush = accentBrush;
                ScalingFitCard.Background = subtleAccentBrush;
                ActiveScalingBadgeText.Text = "Fit to Screen";
                break;
            case ScalingMode.Stretch:
                ScalingStretchCard.BorderBrush = accentBrush;
                ScalingStretchCard.Background = subtleAccentBrush;
                ActiveScalingBadgeText.Text = "Stretch";
                break;
            case ScalingMode.Fill:
            default:
                ScalingCropCard.BorderBrush = accentBrush;
                ScalingCropCard.Background = subtleAccentBrush;
                ActiveScalingBadgeText.Text = "Crop to Center";
                break;
        }
    }

    private async void ScalingMode_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement card && card.Tag is string modeStr)
        {
            var app = (App)Application.Current;
            var mode = modeStr switch
            {
                "Fit" => ScalingMode.Fit,
                "Stretch" => ScalingMode.Stretch,
                _ => ScalingMode.Fill
            };

            app.SettingsService.Settings.Display.DefaultScalingMode = mode;
            await app.SettingsService.SaveAsync();

            UpdateScalingModeUI(mode);
            Log.Information("User switched default video scaling mode to: {Mode}", mode);

            // Re-apply scaling to all active wallpapers immediately
            foreach (var m in app.MonitorService.Monitors)
            {
                var active = app.WallpaperService.GetActiveWallpaper(m.DeviceId);
                if (active != null)
                {
                    await app.WallpaperService.SetWallpaperAsync(m.DeviceId, active);
                }
            }
        }
    }

    private void UpdatePlacementModeUI(string mode)
    {
        var accentBrush = (System.Windows.Media.Brush)FindResource("MxAccentBrush");
        var subtleAccentBrush = (System.Windows.Media.Brush)FindResource("MxAccentSubtleBrush");
        var defaultBorderBrush = (System.Windows.Media.Brush)FindResource("CardStrokeColorDefaultBrush");
        var defaultBgBrush = (System.Windows.Media.Brush)FindResource("CardBackgroundFillColorDefaultBrush");

        ModePerDisplayCard.BorderBrush = defaultBorderBrush;
        ModePerDisplayCard.Background = defaultBgBrush;
        ModeDuplicateCard.BorderBrush = defaultBorderBrush;
        ModeDuplicateCard.Background = defaultBgBrush;
        ModeSpanCard.BorderBrush = defaultBorderBrush;
        ModeSpanCard.Background = defaultBgBrush;

        if (string.Equals(mode, "Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            ModeDuplicateCard.BorderBrush = accentBrush;
            ModeDuplicateCard.Background = subtleAccentBrush;
            ActiveModeBadgeText.Text = "Duplicate / Mirror";
        }
        else if (string.Equals(mode, "Span", StringComparison.OrdinalIgnoreCase))
        {
            ModeSpanCard.BorderBrush = accentBrush;
            ModeSpanCard.Background = subtleAccentBrush;
            ActiveModeBadgeText.Text = "Span Screen";
        }
        else
        {
            ModePerDisplayCard.BorderBrush = accentBrush;
            ModePerDisplayCard.Background = subtleAccentBrush;
            ActiveModeBadgeText.Text = "Per-Display";
        }
    }

    private async void PlacementMode_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement card && card.Tag is string mode)
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Display.PlacementMode = mode;
            await app.SettingsService.SaveAsync();

            UpdatePlacementModeUI(mode);
            Log.Information("User switched wallpaper placement mode to: {Mode}", mode);

            // If Duplicate is selected, duplicate active wallpaper across all monitors
            if (string.Equals(mode, "Duplicate", StringComparison.OrdinalIgnoreCase))
            {
                var active = app.WallpaperService.GetAllActiveWallpapers().FirstOrDefault();
                if (active != null)
                {
                    foreach (var m in app.MonitorService.Monitors)
                    {
                        await app.WallpaperService.SetWallpaperAsync(m.DeviceId, active);
                    }
                    RefreshDisplays();
                }
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app.MonitorService.Refresh();
        RefreshDisplays();
    }

    private void IdentifyButton_Click(object sender, RoutedEventArgs e)
    {
        Log.Information("Identify displays clicked");
        DisplayIdentifyHelper.ShowIdentifyOverlays();
    }

    public void RefreshDisplays()
    {
        _loadingEffects = true;
        var app = (App)Application.Current;
        var monitors = app.MonitorService.Monitors;

        DisplaysCountText.Text = $"{monitors.Count} display{(monitors.Count != 1 ? "s" : "")} detected";

        var list = new List<DisplayViewModel>();
        foreach (var m in monitors)
        {
            var active = app.WallpaperService.GetActiveWallpaper(m.DeviceId);
            bool hasAudio = active != null && active.Type is WallpaperType.Video or WallpaperType.Scene;
            bool isMuted = app.WallpaperService.IsMonitorMuted(m.DeviceId);
            int volume = app.WallpaperService.GetMonitorVolume(m.DeviceId);

            // Load per-monitor effects
            app.SettingsService.Settings.MonitorAssignments.TryGetValue(m.DeviceId, out var assignment);
            var fx = assignment?.Effects;
            var global = app.SettingsService.Settings.Effects;

            int brightness = (int)Math.Round((fx?.Brightness ?? global.Brightness) * 100f);
            if (brightness > 170)
            {
                _oledWarned.Add(m.DeviceId);
            }

            list.Add(new DisplayViewModel
            {
                DeviceId = m.DeviceId,
                DisplayName = m.DisplayName,
                PrimaryVisibility = m.IsPrimary ? Visibility.Visible : Visibility.Collapsed,
                SpecsString = $"{m.Bounds.Width}×{m.Bounds.Height} @ {m.RefreshRate}Hz  •  DPI: {(int)(m.DpiScale * 100)}%  •  Pos: ({m.Bounds.X}, {m.Bounds.Y})",
                ActiveWallpaperString = active != null ? $"Active Wallpaper: {active.Name} ({active.Type})" : "Active Wallpaper: None",
                HasAudioControls = hasAudio,
                IsMuted = isMuted,
                Volume = volume,
                BrightnessValue  = brightness,
                ContrastValue    = (int)Math.Round((fx?.Contrast     ?? global.Contrast)     * 100f),
                SaturationValue  = (int)Math.Round((fx?.Saturation   ?? global.Saturation)   * 100f),
                ColorTempValue   = fx?.ColorTemperature ?? global.ColorTemperature,
                HasEffectsOverride = fx?.HasAnyOverride == true,
            });
        }

        DisplaysList.ItemsSource = list;
        _loadingEffects = false;
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            bool currentMuted = app.WallpaperService.IsMonitorMuted(deviceId);
            app.WallpaperService.SetMonitorMuted(deviceId, !currentMuted);
            RefreshDisplays();
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            int newVolume = (int)Math.Round(e.NewValue);
            app.WallpaperService.SetMonitorVolume(deviceId, newVolume);

            if (element.Parent is Panel panel)
            {
                var textBlock = panel.Children.OfType<TextBlock>().LastOrDefault();
                if (textBlock != null)
                {
                    textBlock.Text = $"{newVolume}%";
                }
            }
        }
    }

    private async void ClearWallpaper_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            try
            {
                await app.WallpaperService.RemoveWallpaperAsync(deviceId);
                RefreshDisplays();
                Log.Information("Cleared wallpaper for display {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear wallpaper for display {DeviceId}", deviceId);
            }
        }
    }

    // ── Per-Monitor Effects ──────────────────────────────────────────────────

    private MonitorEffectsSettings GetOrCreateMonitorEffects(string deviceId)
    {
        var app = (App)Application.Current;
        var assignments = app.SettingsService.Settings.MonitorAssignments;
        if (!assignments.TryGetValue(deviceId, out var assignment))
        {
            assignment = new MonitorAssignment();
            assignments[deviceId] = assignment;
        }
        assignment.Effects ??= new MonitorEffectsSettings();
        return assignment.Effects;
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingEffects) return;
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            int val = (int)Math.Round(e.NewValue);

            if (val > 170)
            {
                // Only warn once per crossing — reset when they go back below 170
                if (_oledWarned.Add(deviceId))
                {
                    Views.ModernDialogWindow.Warning(
                        "High Brightness Warning",
                        "Brightness above 170% may accelerate OLED display wear",
                        "Consider keeping brightness at or below 170% for OLED/AMOLED screens to reduce panel stress.");
                }
            }
            else
            {
                // Reset so warning fires again next time they cross the threshold
                _oledWarned.Remove(deviceId);
            }

            var fx = GetOrCreateMonitorEffects(deviceId);
            fx.Brightness = Math.Clamp(val / 100f, 0.05f, 2.0f);
            ApplyAndSaveMonitor(deviceId);
            UpdateEffectLabel(element, $"{val}%");
        }
    }

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingEffects) return;
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            int val = (int)Math.Round(e.NewValue);
            var fx = GetOrCreateMonitorEffects(deviceId);
            fx.Contrast = Math.Clamp(val / 100f, 0.05f, 2.5f);
            ApplyAndSaveMonitor(deviceId);
            UpdateEffectLabel(element, $"{val}%");
        }
    }

    private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingEffects) return;
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            int val = (int)Math.Round(e.NewValue);
            var fx = GetOrCreateMonitorEffects(deviceId);
            fx.Saturation = Math.Clamp(val / 100f, 0.0f, 3.0f);
            ApplyAndSaveMonitor(deviceId);
            UpdateEffectLabel(element, $"{val}%");
        }
    }

    private void ColorTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingEffects) return;
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            int val = (int)Math.Round(e.NewValue);
            var fx = GetOrCreateMonitorEffects(deviceId);
            fx.ColorTemperature = Math.Clamp(val, -50, 50);
            ApplyAndSaveMonitor(deviceId);
            string label = val == 0 ? "Neutral" : (val > 0 ? $"+{val} Warm" : $"{val} Cool");
            UpdateEffectLabel(element, label);
        }
    }

    private void ResetMonitorEffects_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string deviceId)
        {
            var app = (App)Application.Current;
            if (app.SettingsService.Settings.MonitorAssignments.TryGetValue(deviceId, out var assignment))
            {
                assignment.Effects = null;
            }
            app.WallpaperService.ApplyEffectsToMonitor(deviceId);
            _ = app.SettingsService.SaveAsync();
            RefreshDisplays();
        }
    }

    private void ApplyAndSaveMonitor(string deviceId)
    {
        var app = (App)Application.Current;
        app.WallpaperService.ApplyEffectsToMonitor(deviceId);
        _ = app.SettingsService.SaveAsync();
    }

    private static void UpdateEffectLabel(FrameworkElement slider, string text)
    {
        // Walk up to find sibling TextBlock in parent StackPanel
        if (slider.Parent is Panel panel)
        {
            var tb = panel.Children.OfType<TextBlock>().LastOrDefault();
            if (tb != null) tb.Text = text;
        }
    }
}
