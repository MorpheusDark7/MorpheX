using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.IO;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using System.Windows;
using Application = System.Windows.Application;
using System.Windows.Controls;
using System.Windows.Input;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using MorpheX.Views;
using Serilog;

namespace MorpheX;

public partial class SettingsPage : Page
{
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;

        var app = (App)Application.Current;
        var settings = app.SettingsService.Settings;

        StartWithWindowsToggle.IsChecked = app.StartupService.IsRegistered;
        StartMinimizedToggle.IsChecked = settings.General.StartMinimized;
        CheckForUpdatesToggle.IsChecked = settings.General.CheckForUpdates;

        string currentScaling = settings.Display.DefaultScalingMode.ToString();
        foreach (ComboBoxItem item in ScalingModeCombo.Items)
        {
            if ((string)item.Tag == currentScaling)
            {
                ScalingModeCombo.SelectedItem = item;
                break;
            }
        }
        if (ScalingModeCombo.SelectedItem == null && ScalingModeCombo.Items.Count > 0)
        {
            ScalingModeCombo.SelectedIndex = 0;
        }

        string currentTheme = settings.Appearance.Theme ?? "System";
        foreach (ComboBoxItem item in ThemeModeCombo.Items)
        {
            if (string.Equals((string)item.Tag, currentTheme, StringComparison.OrdinalIgnoreCase))
            {
                ThemeModeCombo.SelectedItem = item;
                break;
            }
        }
        if (ThemeModeCombo.SelectedItem == null && ThemeModeCombo.Items.Count > 0)
        {
            ThemeModeCombo.SelectedIndex = 0;
        }

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

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "0.1.64"}";

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

    private async void ScalingModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (ScalingModeCombo.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<ScalingMode>((string)item.Tag, out var mode))
        {
            var app = (App)Application.Current;
            app.WallpaperService.ApplyScalingToAll(mode);
        }
    }

    private async void CheckForUpdatesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var app = (App)Application.Current;
        app.SettingsService.Settings.General.CheckForUpdates = CheckForUpdatesToggle.IsChecked == true;
        await app.SettingsService.SaveAsync();
    }

    private async void ThemeModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (ThemeModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string mode)
        {
            var app = (App)Application.Current;
            app.SettingsService.Settings.Appearance.Theme = mode;
            await app.SettingsService.SaveAsync();
            Services.ThemeService.ApplyTheme(mode);
        }
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
            app.PlaylistService.UpdateSettings();
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

    #region Global Keybinds
    private void OpenKeybinds_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.NavigateTo(typeof(KeybindsPage));
        }
    }
    #endregion



    #region Diagnostics
    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MorpheX", "logs");
        Directory.CreateDirectory(logDirectory);
        Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });
    }

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var builder = new StringBuilder()
            .AppendLine($"MorpheX Live {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}")
            .AppendLine($"Windows: {Environment.OSVersion.VersionString}")
            .AppendLine($"Displays: {app.MonitorService.Monitors.Count}")
            .AppendLine($"Library wallpapers: {app.LibraryService.Wallpapers.Count}")
            .AppendLine($"Collections: {app.LibraryService.Collections.Count}")
            .AppendLine($"Manual pause: {app.PlaybackService.IsManuallyPaused}")
            .AppendLine($"Active wallpapers: {app.WallpaperService.GetAllActiveWallpapers().Count}");
        Clipboard.SetText(builder.ToString());
        ModernDialogWindow.Success(
            title:   "Diagnostics",
            heading: "Copied to clipboard",
            message: "Diagnostics information has been copied to the clipboard.");
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
                var curVer = app.UpdateService.CurrentVersion;
                string verStr = curVer.Build >= 0 ? curVer.ToString(3) : curVer.ToString();
                UpdateStatusCard.Visibility = Visibility.Visible;
                UpdateTitleText.Text = "You're up to date!";
                UpdateDescText.Text = $"MorpheX Live v{verStr} is the latest available release.";
                InstallUpdateBtn.Visibility = Visibility.Collapsed;
                UpdateProgressPanel.Visibility = Visibility.Collapsed;
                UpdateStatusCard.BringIntoView();
            }
        }
        catch (Exception ex)
        {
            UpdateStatusCard.Visibility = Visibility.Visible;
            UpdateTitleText.Text = "Check Failed";
            UpdateDescText.Text = "Unable to connect to GitHub. Please check your internet connection.";
            InstallUpdateBtn.Visibility = Visibility.Collapsed;
            UpdateProgressPanel.Visibility = Visibility.Collapsed;
            UpdateStatusCard.BringIntoView();
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
        UpdateStatusCard.BringIntoView();
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
