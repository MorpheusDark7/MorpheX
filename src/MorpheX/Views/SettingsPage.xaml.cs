using System.Reflection;
using System.Windows;
using Application = System.Windows.Application;
using System.Windows.Controls;
using System.Windows.Input;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX;

public partial class SettingsPage : Page
{
    private bool _isInitializing = true;
    private string? _recordingTarget;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += Page_PreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        var app = (App)Application.Current;
        var settings = app.SettingsService.Settings;

        StartWithWindowsToggle.IsChecked = app.StartupService.IsRegistered;
        StartMinimizedToggle.IsChecked = settings.General.StartMinimized;
        ShowSystemStatsToggle.IsChecked = settings.Appearance.ShowSystemStats;
        CheckForUpdatesToggle.IsChecked = settings.General.CheckForUpdates;

        if (app.AvailableUpdate != null)
        {
            ShowUpdateCard(app.AvailableUpdate);
        }

        HardwareAccelerationToggle.IsChecked = settings.Playback.HardwareAcceleration;
        var currentRule = settings.Performance.PauseRule.ToString();
        foreach (ComboBoxItem item in PauseRuleCombo.Items)
        {
            if ((string)item.Tag == currentRule)
            {
                PauseRuleCombo.SelectedItem = item;
                break;
            }
        }
        PauseBatteryToggle.IsChecked = settings.Performance.PauseOnBattery;
        ResumePositionToggle.IsChecked = settings.Playback.ResumeFromPreviousPosition;

        PlaylistEnabledToggle.IsChecked = settings.Playlist.Enabled;
        string currentInterval = settings.Playlist.IntervalMinutes.ToString();
        foreach (ComboBoxItem item in PlaylistIntervalCombo.Items)
        {
            if ((string)item.Tag == currentInterval)
            {
                PlaylistIntervalCombo.SelectedItem = item;
                break;
            }
        }
        if (PlaylistIntervalCombo.SelectedItem == null && PlaylistIntervalCombo.Items.Count > 4)
        {
            PlaylistIntervalCombo.SelectedIndex = 4;
        }

        string currentSource = settings.Playlist.Source.ToString();
        foreach (ComboBoxItem item in PlaylistSourceCombo.Items)
        {
            if ((string)item.Tag == currentSource)
            {
                PlaylistSourceCombo.SelectedItem = item;
                break;
            }
        }

        string currentOrder = settings.Playlist.Order.ToString();
        foreach (ComboBoxItem item in PlaylistOrderCombo.Items)
        {
            if ((string)item.Tag == currentOrder)
            {
                PlaylistOrderCombo.SelectedItem = item;
                break;
            }
        }

        PlaylistStartupToggle.IsChecked = settings.Playlist.ChangeOnStartup;
        PlaylistSkipPausedToggle.IsChecked = settings.Playlist.SkipWhenPaused;

        UpdateHotkeyButtons();

        AudioEnabledToggle.IsChecked = settings.Audio.Enabled;
        VolumeSlider.Value = settings.Audio.Volume;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "0.1.2"}";

        if (app.AvailableUpdate != null)
        {
            ShowUpdateCard(app.AvailableUpdate);
        }

        _isInitializing = false;
    }

    #region General
    private async void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.General.StartWithWindows = StartWithWindowsToggle.IsChecked == true;

        if (StartWithWindowsToggle.IsChecked == true)
            app.StartupService.Register();
        else
            app.StartupService.Unregister();

        await app.SettingsService.SaveAsync();
    }

    private async void StartMinimizedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.General.StartMinimized = StartMinimizedToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void ShowSystemStatsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Appearance.ShowSystemStats = ShowSystemStatsToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();

        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.UpdateStatsBarVisibility();
        }
    }

    private async void CheckForUpdatesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.General.CheckForUpdates = CheckForUpdatesToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }
    #endregion

    #region Performance
    private async void HardwareAccelerationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        bool enabled = HardwareAccelerationToggle.IsChecked == true;
        app.SettingsService.Settings.Playback.HardwareAcceleration = enabled;
        await app.SettingsService.SaveAsync();

        Log.Information("Hardware acceleration toggled to: {Enabled}. Reloading wallpapers...", enabled);
        await app.WallpaperService.ReloadActiveWallpapersAsync();
    }

    private async void PauseRuleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PauseRuleCombo.SelectedItem is ComboBoxItem selected && selected.Tag is string tag)
        {
            var app = (App)Application.Current;
            if (Enum.TryParse<PauseRule>(tag, out var rule))
            {
                app.SettingsService.Settings.Performance.PauseRule = rule;
                await app.SettingsService.SaveAsync();
                Log.Information("Pause rule changed to: {Rule}", rule);
                if (app.PlaybackService.IsManuallyPaused)
                {
                    app.PlaybackService.Resume();
                }
                else
                {
                    app.PlaybackService.PollNow();
                }
            }
        }
    }

    private async void PauseBatteryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Performance.PauseOnBattery = PauseBatteryToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
        app.PlaybackService.PollNow();
    }

    private async void ResumePositionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playback.ResumeFromPreviousPosition = ResumePositionToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }
    #endregion

    #region Playlist & Auto-Rotation
    private async void PlaylistEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playlist.Enabled = PlaylistEnabledToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
        app.PlaylistService.UpdateSettings();
    }

    private async void PlaylistIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PlaylistIntervalCombo.SelectedItem is ComboBoxItem item && int.TryParse((string)item.Tag, out int minutes))
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Playlist.IntervalMinutes = minutes;
            await app.SettingsService.SaveAsync();
            app.PlaylistService.UpdateSettings();
        }
    }

    private async void PlaylistSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PlaylistSourceCombo.SelectedItem is ComboBoxItem item && Enum.TryParse<PlaylistSource>((string)item.Tag, out var src))
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Playlist.Source = src;
            await app.SettingsService.SaveAsync();
        }
    }

    private async void PlaylistOrderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PlaylistOrderCombo.SelectedItem is ComboBoxItem item && Enum.TryParse<PlaylistOrder>((string)item.Tag, out var order))
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Playlist.Order = order;
            await app.SettingsService.SaveAsync();
        }
    }

    private async void PlaylistStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playlist.ChangeOnStartup = PlaylistStartupToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void PlaylistSkipPausedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.Playlist.SkipWhenPaused = PlaylistSkipPausedToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void RotateNowBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        await app.PlaylistService.TriggerNextWallpaperAsync();
    }
    #endregion

    #region Global Hotkeys
    private void UpdateHotkeyButtons()
    {
        var hotkeys = ((App)Application.Current).SettingsService.Settings.Hotkeys;
        HotkeyPauseBtn.Content = _recordingTarget == "PauseResume" ? "Press shortcut..." : hotkeys.PauseResume.DisplayText;
        HotkeyMuteBtn.Content = _recordingTarget == "MuteUnmute" ? "Press shortcut..." : hotkeys.MuteUnmute.DisplayText;
        HotkeyNextBtn.Content = _recordingTarget == "NextWallpaper" ? "Press shortcut..." : hotkeys.NextWallpaper.DisplayText;
    }

    private void HotkeyPauseBtn_Click(object sender, RoutedEventArgs e)
    {
        _recordingTarget = "PauseResume";
        UpdateHotkeyButtons();
    }

    private async void HotkeyPauseClearBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var binding = app.SettingsService.Settings.Hotkeys.PauseResume;
        binding.Key = "None";
        binding.Modifiers = "None";
        binding.Enabled = false;

        app.HotkeyService.UpdateBindings();
        await app.SettingsService.SaveAsync();
        UpdateHotkeyButtons();
    }

    private void HotkeyMuteBtn_Click(object sender, RoutedEventArgs e)
    {
        _recordingTarget = "MuteUnmute";
        UpdateHotkeyButtons();
    }

    private async void HotkeyMuteClearBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var binding = app.SettingsService.Settings.Hotkeys.MuteUnmute;
        binding.Key = "None";
        binding.Modifiers = "None";
        binding.Enabled = false;

        app.HotkeyService.UpdateBindings();
        await app.SettingsService.SaveAsync();
        UpdateHotkeyButtons();
    }

    private void HotkeyNextBtn_Click(object sender, RoutedEventArgs e)
    {
        _recordingTarget = "NextWallpaper";
        UpdateHotkeyButtons();
    }

    private async void HotkeyNextClearBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var binding = app.SettingsService.Settings.Hotkeys.NextWallpaper;
        binding.Key = "None";
        binding.Modifiers = "None";
        binding.Enabled = false;

        app.HotkeyService.UpdateBindings();
        await app.SettingsService.SaveAsync();
        UpdateHotkeyButtons();
    }

    private void Page_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingTarget == null) return;

        if (e.Key == Key.Escape)
        {
            _recordingTarget = null;
            UpdateHotkeyButtons();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin or
            Key.System)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        var modList = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) modList.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) modList.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) modList.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) modList.Add("Win");
        string modStr = modList.Count > 0 ? string.Join("+", modList) : "None";

        var app = (App)Application.Current;
        var hotkeys = app.SettingsService.Settings.Hotkeys;

        HotkeyBinding? targetBinding = _recordingTarget switch
        {
            "PauseResume" => hotkeys.PauseResume,
            "MuteUnmute" => hotkeys.MuteUnmute,
            "NextWallpaper" => hotkeys.NextWallpaper,
            _ => null
        };

        if (targetBinding != null)
        {
            targetBinding.Key = key.ToString();
            targetBinding.Modifiers = modStr;
            targetBinding.Enabled = true;

            app.HotkeyService.UpdateBindings();
            _ = app.SettingsService.SaveAsync();
            Log.Information("Mapped hotkey for '{Action}': {Binding}", _recordingTarget, targetBinding.DisplayText);
        }

        _recordingTarget = null;
        UpdateHotkeyButtons();
        e.Handled = true;
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

    private async void VolumeSlider_ValueChanged(object sender,
        RoutedPropertyChangedEventArgs<double> e)
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

    #region Updates
    private UpdateInfo? _pendingUpdate;

    private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        CheckUpdatesBtn.IsEnabled = false;
        CheckUpdatesBtn.Content = "Checking...";

        try
        {
            var update = await app.UpdateService.CheckForUpdatesAsync();
            if (update != null)
            {
                ShowUpdateCard(update);
            }
            else
            {
                UpdateStatusCard.Visibility = Visibility.Visible;
                UpdateTitleText.Text = "You're up to date!";
                UpdateDescText.Text = $"MorpheX Live v{app.UpdateService.CurrentVersion} is the latest available release.";
                InstallUpdateBtn.Visibility = Visibility.Collapsed;
                UpdateProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusCard.Visibility = Visibility.Visible;
            UpdateTitleText.Text = "Check Failed";
            UpdateDescText.Text = "Unable to connect to GitHub. Please check your internet connection.";
            InstallUpdateBtn.Visibility = Visibility.Collapsed;
            UpdateProgressPanel.Visibility = Visibility.Collapsed;
            Log.Warning(ex, "Update check failed from Settings page");
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            CheckUpdatesBtn.Content = "Check for updates";
        }
    }

    private void ShowUpdateCard(UpdateInfo update)
    {
        _pendingUpdate = update;
        UpdateStatusCard.Visibility = Visibility.Visible;
        UpdateTitleText.Text = $"🎉 {update.Title} Available!";
        UpdateDescText.Text = $"Version {update.TagName} ({update.FormattedSize}) is ready to download and install.";
        InstallUpdateBtn.Visibility = Visibility.Visible;
        InstallUpdateBtn.IsEnabled = true;
        InstallUpdateBtn.Content = "1-Click Update Now";
        UpdateProgressPanel.Visibility = Visibility.Collapsed;
    }

    private async void InstallUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;

        var app = (App)Application.Current;
        InstallUpdateBtn.IsEnabled = false;
        InstallUpdateBtn.Content = "Updating...";
        UpdateProgressPanel.Visibility = Visibility.Visible;
        UpdateProgressBar.Value = 0;
        UpdateProgressPercent.Text = "0%";
        UpdateProgressText.Text = "Downloading update from GitHub...";

        var progress = new Progress<double>(pct =>
        {
            UpdateProgressBar.Value = pct;
            UpdateProgressPercent.Text = $"{(int)pct}%";
        });

        try
        {
            string installerPath = await app.UpdateService.DownloadInstallerAsync(_pendingUpdate, progress);
            UpdateProgressText.Text = "Launching installer...";
            await Task.Delay(500);

            app.UpdateService.LaunchInstaller(installerPath, silent: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download and execute update installer");
            UpdateProgressText.Text = "Download failed. Please check internet connection.";
            InstallUpdateBtn.IsEnabled = true;
            InstallUpdateBtn.Content = "Retry Update";
        }
    }
    #endregion
}
