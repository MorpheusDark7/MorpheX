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

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, true, cornerRadius: 12, isLightTheme: isLight);

        HeaderTitleText.Foreground = WidgetStyles.GetWidgetSubtleForeground(isLight);
        NoteText.Foreground = WidgetStyles.GetWidgetForeground(isLight);
        NoteText.CaretBrush = isLight
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x28, 0x20))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
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
