using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MorpheX.Core.Configuration;
using Serilog;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;

namespace MorpheX;

public partial class KeybindsPage : Page
{
    private string? _recordingTarget;

    public KeybindsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateHotkeyButtons();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _recordingTarget = null;
    }

    private void UpdateHotkeyButtons()
    {
        if (Application.Current is not App app || app.SettingsService?.Settings?.Hotkeys == null)
            return;

        var hotkeys = app.SettingsService.Settings.Hotkeys;

        // Navigation
        SetButtonState(HotkeyOpenLibraryBtn, hotkeys.OpenLibrary, "OpenLibrary");
        SetButtonState(HotkeyOpenFavoritesBtn, hotkeys.OpenFavorites, "OpenFavorites");
        SetButtonState(HotkeyOpenEffectsBtn, hotkeys.OpenEffects, "OpenEffects");
        SetButtonState(HotkeyToggleAppBtn, hotkeys.ToggleAppWindow, "ToggleAppWindow");

        // Playback & Audio
        SetButtonState(HotkeyPauseBtn, hotkeys.PauseResume, "PauseResume");
        SetButtonState(HotkeyMuteBtn, hotkeys.MuteUnmute, "MuteUnmute");
        SetButtonState(HotkeyNextBtn, hotkeys.NextWallpaper, "NextWallpaper");
        SetButtonState(HotkeyPrevBtn, hotkeys.PrevWallpaper, "PrevWallpaper");
        SetButtonState(HotkeyRandomBtn, hotkeys.RandomWallpaper, "RandomWallpaper");
        SetButtonState(HotkeyFavoriteBtn, hotkeys.FavoriteCurrent, "FavoriteCurrent");

        // Power User & Desktop
        SetButtonState(HotkeyBossKeyBtn, hotkeys.BossKey, "BossKey");
        SetButtonState(HotkeyToggleWidgetsBtn, hotkeys.ToggleWidgets, "ToggleWidgets");
        SetButtonState(HotkeyResetEffectsBtn, hotkeys.ResetEffects, "ResetEffects");
    }

    private void SetButtonState(Button? btn, HotkeyBinding? binding, string targetKey)
    {
        if (btn == null) return;

        if (_recordingTarget == targetKey)
        {
            btn.Content = "Press shortcut...";
            if (TryFindResource("MxAccentBrush") is Brush b)
                btn.Foreground = b;
            btn.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            btn.Content = binding?.DisplayText ?? "Not Assigned";
            if (TryFindResource("TextFillColorPrimaryBrush") is Brush b)
                btn.Foreground = b;
            else
                btn.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            btn.FontWeight = FontWeights.Normal;
        }
    }

    private void HotkeyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string target)
        {
            _recordingTarget = _recordingTarget == target ? null : target;
            UpdateHotkeyButtons();
            Focus();
        }
    }

    private async void HotkeyClearBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string target)
        {
            var app = (App)Application.Current;
            var hotkeys = app.SettingsService.Settings.Hotkeys;
            var binding = GetBinding(hotkeys, target);
            if (binding != null)
            {
                binding.Key = "None";
                binding.Modifiers = "None";
                binding.Enabled = false;

                app.HotkeyService.UpdateBindings();
                await app.SettingsService.SaveAsync();
                Log.Information("Cleared global hotkey for '{Action}'", target);
            }

            if (_recordingTarget == target)
            {
                _recordingTarget = null;
            }
            UpdateHotkeyButtons();
        }
    }

    private HotkeyBinding? GetBinding(HotkeySettings hotkeys, string target) => target switch
    {
        "OpenLibrary" => hotkeys.OpenLibrary,
        "OpenFavorites" => hotkeys.OpenFavorites,
        "OpenEffects" => hotkeys.OpenEffects,
        "ToggleAppWindow" => hotkeys.ToggleAppWindow,

        "PauseResume" => hotkeys.PauseResume,
        "MuteUnmute" => hotkeys.MuteUnmute,
        "NextWallpaper" => hotkeys.NextWallpaper,
        "PrevWallpaper" => hotkeys.PrevWallpaper,
        "RandomWallpaper" => hotkeys.RandomWallpaper,
        "FavoriteCurrent" => hotkeys.FavoriteCurrent,

        "BossKey" => hotkeys.BossKey,
        "ToggleWidgets" => hotkeys.ToggleWidgets,
        "ResetEffects" => hotkeys.ResetEffects,
        _ => null
    };

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
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
        var targetBinding = GetBinding(hotkeys, _recordingTarget);

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
}
