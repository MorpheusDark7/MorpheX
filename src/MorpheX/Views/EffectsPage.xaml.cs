using System.Windows;
using System.Windows.Controls;
using MorpheX.Services;
using Application = System.Windows.Application;

namespace MorpheX;

public partial class EffectsPage : Page
{
    private bool _isInitializing = true;
    private bool _effectsThrottling;

    public EffectsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        var app = (App)Application.Current;
        var settings = app.SettingsService.Settings;

        // Visual Effects
        BrightnessSlider.Value = settings.Effects.Brightness * 100.0;
        ContrastSlider.Value   = settings.Effects.Contrast   * 100.0;
        SaturationSlider.Value = settings.Effects.Saturation * 100.0;
        ColorTempSlider.Value  = settings.Effects.ColorTemperature;
        UpdateEffectsLabels();

        // Playback
        FadeInToggle.IsChecked = settings.Playback.FadeInOnLoad;
        string rateTag = settings.Playback.PlaybackRate.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        foreach (ComboBoxItem item in PlaybackRateCombo.Items)
        {
            if ((string)item.Tag == rateTag || Math.Abs(float.Parse((string)item.Tag,
                System.Globalization.CultureInfo.InvariantCulture) - settings.Playback.PlaybackRate) < 0.01f)
            {
                PlaybackRateCombo.SelectedItem = item;
                break;
            }
        }
        if (PlaybackRateCombo.SelectedItem == null) PlaybackRateCombo.SelectedIndex = 3; // 1×

        // Audio
        AudioEnabledToggle.IsChecked = settings.Audio.Enabled;
        VolumeSlider.Value = settings.Audio.Volume;

        // Ambient Dim
        AmbientDimToggle.IsChecked = settings.AmbientDim.Enabled;
        string idleTag = settings.AmbientDim.IdleMinutes.ToString();
        foreach (ComboBoxItem item in AmbientDimIdleCombo.Items)
        {
            if ((string)item.Tag == idleTag)
            {
                AmbientDimIdleCombo.SelectedItem = item;
                break;
            }
        }
        if (AmbientDimIdleCombo.SelectedItem == null) AmbientDimIdleCombo.SelectedIndex = 1; // 5 min
        DimLevelSlider.Value = Math.Round(settings.AmbientDim.DimLevel * 100.0);
        DimLevelValueText.Text = $"{(int)DimLevelSlider.Value}%";

        _isInitializing = false;
    }

    #region Wallpaper Visual Effects

    private void UpdateEffectsLabels()
    {
        BrightnessValueText.Text = $"{(int)BrightnessSlider.Value}%";
        ContrastValueText.Text   = $"{(int)ContrastSlider.Value}%";
        SaturationValueText.Text = $"{(int)SaturationSlider.Value}%";
        int t = (int)ColorTempSlider.Value;
        ColorTempValueText.Text  = t == 0 ? "Neutral" : (t > 0 ? $"+{t} Warm" : $"{t} Cool");
    }

    private void ApplyEffectsNow()
    {
        if (_effectsThrottling) return;
        _effectsThrottling = true;

        Dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(80);
            var app = (App)Application.Current;
            var fx = app.SettingsService.Settings.Effects;
            fx.Brightness       = (float)(BrightnessSlider.Value / 100.0);
            fx.Contrast         = (float)(ContrastSlider.Value   / 100.0);
            fx.Saturation       = (float)(SaturationSlider.Value / 100.0);
            fx.ColorTemperature = (int)ColorTempSlider.Value;
            UpdateEffectsLabels();
            app.WallpaperService.ApplyEffectsToAll();
            await app.SettingsService.SaveAsync();
            _effectsThrottling = false;
        });
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!_isInitializing) ApplyEffectsNow(); else BrightnessValueText.Text = $"{(int)e.NewValue}%"; }

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!_isInitializing) ApplyEffectsNow(); else ContrastValueText.Text = $"{(int)e.NewValue}%"; }

    private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!_isInitializing) ApplyEffectsNow(); else SaturationValueText.Text = $"{(int)e.NewValue}%"; }

    private void ColorTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitializing) ApplyEffectsNow();
        else { int t = (int)e.NewValue; ColorTempValueText.Text = t == 0 ? "Neutral" : (t > 0 ? $"+{t} Warm" : $"{t} Cool"); }
    }

    private async void ResetEffectsBtn_Click(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        BrightnessSlider.Value = 100;
        ContrastSlider.Value   = 100;
        SaturationSlider.Value = 100;
        ColorTempSlider.Value  = 0;
        _isInitializing = false;

        var app = (App)Application.Current;
        var fx = app.SettingsService.Settings.Effects;
        fx.Brightness = 1.0f; fx.Contrast = 1.0f; fx.Saturation = 1.0f; fx.ColorTemperature = 0;
        UpdateEffectsLabels();
        app.WallpaperService.ApplyEffectsToAll();
        await app.SettingsService.SaveAsync();
    }

    #endregion

    #region Playback

    private async void PlaybackRateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PlaybackRateCombo.SelectedItem is not ComboBoxItem item) return;
        float rate = float.Parse((string)item.Tag, System.Globalization.CultureInfo.InvariantCulture);
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playback.PlaybackRate = rate;
        app.WallpaperService.SetPlaybackRate(rate);
        await app.SettingsService.SaveAsync();
    }

    private async void FadeInToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playback.FadeInOnLoad = FadeInToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    #endregion

    #region Audio

    private async void AudioEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Audio.Enabled = AudioEnabledToggle.IsChecked == true;

        float volume = AudioEnabledToggle.IsChecked == true ? (float)VolumeSlider.Value / 100f : -1f;
        app.WallpaperService.SetVolume(volume);

        await app.SettingsService.SaveAsync();
    }

    private async void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Audio.Volume = (int)VolumeSlider.Value;

        if (app.SettingsService.Settings.Audio.Enabled)
        {
            app.WallpaperService.SetVolume((float)VolumeSlider.Value / 100f);
        }

        await app.SettingsService.SaveAsync();
    }

    #endregion

    #region Ambient Dim

    private async void AmbientDimToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.AmbientDim.Enabled = AmbientDimToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void AmbientDimIdleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (AmbientDimIdleCombo.SelectedItem is not ComboBoxItem item) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.AmbientDim.IdleMinutes = int.Parse((string)item.Tag);
        await app.SettingsService.SaveAsync();
    }

    private async void DimLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.AmbientDim.DimLevel = (float)(e.NewValue / 100.0);
        DimLevelValueText.Text = $"{(int)e.NewValue}%";
        await app.SettingsService.SaveAsync();
    }

    #endregion
}
