using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class NotesWidget : Window
{
    private readonly WidgetSettings _settings;
    private DispatcherTimer? _saveDebounce;
    private bool _loading = true;

    public NotesWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left   = settings.NotesX;
        Top    = settings.NotesY;
        Width  = Math.Max(140, settings.NotesWidth);
        Height = Math.Max(100, settings.NotesHeight);

        ApplyTheme();
        UpdateLockState();

        Loaded  += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    public void RefreshDisplay() => ApplyTheme();

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.NotesShowBackground, cornerRadius: 12, isLightTheme: isLight);

        NoteText.FontSize = _settings.NotesFontSize switch { 0 => 11, 2 => 16, _ => 13 };

        var (textBrush, headerBrush) = _settings.NotesColorMode switch
        {
            1 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 56, 189, 248))),
            2 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 74, 222, 128))),
            3 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 192, 132, 252))),
            4 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 251, 146, 60))),
            5 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 240, 138)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 254, 240, 138))),
            _ => (WidgetStyles.GetWidgetForeground(isLight), WidgetStyles.GetWidgetSubtleForeground(isLight))
        };

        HeaderTitleText.Foreground = headerBrush;
        NoteText.Foreground = textBrush;
        NoteText.CaretBrush = textBrush;

        Opacity = Math.Clamp(_settings.NotesOpacity > 0 ? _settings.NotesOpacity : 1.0, 0.2, 1.0);
        MorpheX.Services.WidgetWindowHelper.RefreshWidgetSurface(this);

        if (ToggleCardMenu != null)
            ToggleCardMenu.Header = _settings.NotesShowBackground ? "Hide Background Card" : "Show Background Card";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NoteText.Text = _settings.NotesText;
        _loading = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            Save();
        }
    }

    private void NoteText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_loading) return;
        _settings.NotesText = NoteText.Text;
        DebouncedSave();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        Save();
        (Application.Current as App)?.WidgetService.RefreshLockState();
    }

    public void Reposition()
    {
        Left = _settings.NotesX;
        Top = _settings.NotesY;
        if (_settings.NotesWidth > 50) Width = _settings.NotesWidth;
        if (_settings.NotesHeight > 50) Height = _settings.NotesHeight;
        NoteText.Text = _settings.NotesText ?? string.Empty;
        ApplyTheme();
        UpdateLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, HeaderPanel, _settings.WidgetsLocked, hasResizeGrip: true);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.NotesShowBackground = !_settings.NotesShowBackground;
        ApplyTheme();
        Save();
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && int.TryParse(item.Tag?.ToString(), out int c))
        {
            _settings.NotesColorMode = c;
            ApplyTheme();
            Save();
        }
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && int.TryParse(item.Tag?.ToString(), out int sz))
        {
            _settings.NotesFontSize = sz;
            ApplyTheme();
            Save();
        }
    }

    private void Opacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op))
        {
            _settings.NotesOpacity = op;
            ApplyTheme();
            Save();
        }
    }

    private void ReloadWidget_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app?.WidgetService.ReloadWidget(typeof(NotesWidget));
    }

    private void ResetSize_Click(object sender, RoutedEventArgs e)
    {
        Width  = 220;
        Height = 160;
        _settings.NotesWidth  = Width;
        _settings.NotesHeight = Height;
        Save();
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.NotesEnabled = false;
        Save();
        Close();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settings.NotesWidth  = ActualWidth;
        _settings.NotesHeight = ActualHeight;
        DebouncedSave();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.NotesX      = Left;
        _settings.NotesY      = Top;
        _settings.NotesWidth  = ActualWidth;
        _settings.NotesHeight = ActualHeight;
        _settings.NotesText   = NoteText.Text;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.NotesX = Left;
        _settings.NotesY = Top;
    }

    private void DebouncedSave()
    {
        _saveDebounce?.Stop();
        _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); Save(); };
        _saveDebounce.Start();
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
