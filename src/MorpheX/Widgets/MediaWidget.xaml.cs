using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using MorpheX.Services;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;

namespace MorpheX.Widgets;

public partial class MediaWidget : Window
{
    private const int BarCount = 28;
    private const double MinBarHeight = 2.0;

    private readonly WidgetSettings _settings;
    private readonly AudioPeakMeter _peakMeter = new();
    private readonly SystemMediaManager _mediaManager = new();

    private readonly DispatcherTimer _animTimer;
    private readonly DispatcherTimer _mediaTimer;

    private readonly Border[] _bars = new Border[BarCount];
    private readonly double[] _currentHeights = new double[BarCount];
    private readonly double[] _frequencyWeights = new double[BarCount];
    private readonly Random _rnd = new();

    private readonly SolidColorBrush _activeBrush = new(System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
    private readonly SolidColorBrush _idleBrush = new(System.Windows.Media.Color.FromArgb(255, 80, 80, 85));

    private double _time;
    private DispatcherTimer? _saveDebounceTimer;

    public MediaWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.MediaX;
        Top = settings.MediaY;
        Width = Math.Max(240, settings.MediaWidth);
        Height = Math.Max(110, settings.MediaHeight);

        UpdateLockState();

        // Precompute frequency weights across bars (slight curve emphasizing bass/mids)
        for (int i = 0; i < BarCount; i++)
        {
            double norm = (double)i / (BarCount - 1);
            _frequencyWeights[i] = 0.7 + 0.6 * Math.Sin(norm * Math.PI) + (1.0 - norm) * 0.2;
            _currentHeights[i] = MinBarHeight;
        }

        // 30 FPS animation timer for fluid equalizer bars
        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _animTimer.Tick += OnAnimTick;

        // 1-second timer for polling system media track title/artist
        _mediaTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _mediaTimer.Tick += async (_, _) => await RefreshMediaInfoAsync();

        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Build equalizer bar elements
        BarsGrid.Children.Clear();
        var brush = GetVisualizerBrush();
        var glow  = GetBarGlow();
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Border
            {
                Margin = new Thickness(1.5, 0, 1.5, 0),
                CornerRadius = new CornerRadius(3, 3, 1, 1),
                Background = brush,
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = MinBarHeight,
                Effect = glow,
                // Cache mode for GPU-accelerated rendering
                CacheMode = new BitmapCache { RenderAtScale = 1.0 }
            };
            _bars[i] = bar;
            BarsGrid.Children.Add(bar);
        }

        RefreshVisualizerStyle();

        _animTimer.Start();
        _mediaTimer.Start();

        await _mediaManager.InitializeAsync();
        await RefreshMediaInfoAsync();
    }

    public void RefreshVisualizerStyle()
    {
        ApplyAppearance();

        var brush = GetVisualizerBrush();
        var glow  = GetBarGlow();

        if (_settings.VisualizerStyle == 2) // Waveform
        {
            BarsGrid.Visibility = Visibility.Collapsed;
            WavePath.Visibility = Visibility.Visible;
            WavePath.Stroke = brush;
            WavePath.Effect = glow;
        }
        else
        {
            BarsGrid.Visibility = Visibility.Visible;
            WavePath.Visibility = Visibility.Collapsed;

            var valignment = _settings.VisualizerStyle == 1
                ? VerticalAlignment.Center
                : VerticalAlignment.Bottom;

            BarsGrid.VerticalAlignment = valignment;
            for (int i = 0; i < BarCount; i++)
            {
                if (_bars[i] != null)
                {
                    _bars[i].VerticalAlignment = valignment;
                    _bars[i].Background = brush;
                    _bars[i].Effect = glow;
                }
            }
        }
    }

    private void ApplyAppearance()
    {
        WidgetStyles.ApplyFrostedGlass(MainCardBorder, _settings.MediaShowBackground, 14);
        MainCardBorder.Margin = _settings.MediaShowBackground ? new Thickness(8) : new Thickness(2);

        if (_settings.MediaShowTrackDetails)
        {
            HeaderRow.Visibility = Visibility.Visible;
            TrackInfoRow.Visibility = Visibility.Visible;
            ControlsRow.Visibility = Visibility.Visible;
            MinHeight = 110;
        }
        else
        {
            HeaderRow.Visibility = Visibility.Collapsed;
            TrackInfoRow.Visibility = Visibility.Collapsed;
            ControlsRow.Visibility = Visibility.Collapsed;
            MinHeight = 35;
        }

        ToggleCardMenu.Header = _settings.MediaShowBackground ? "🔲  Hide Background Card" : "🔲  Show Background Card";
        ToggleDetailsMenu.Header = _settings.MediaShowTrackDetails ? "🎵  Hide Track Details" : "🎵  Show Track Details";
    }

    private System.Windows.Media.Brush GetVisualizerBrush()
    {
        return _settings.VisualizerColorMode switch
        {
            1 => (System.Windows.Media.Brush)Resources["BarBrushCyan"],
            2 => (System.Windows.Media.Brush)Resources["BarBrushViolet"],
            3 => (System.Windows.Media.Brush)Resources["BarBrushMint"],
            4 => (System.Windows.Media.Brush)Resources["BarBrushAmber"],
            _ => (System.Windows.Media.Brush)Resources["BarBrushWhite"]
        };
    }

    private DropShadowEffect GetBarGlow()
    {
        var (col, op) = _settings.VisualizerColorMode switch
        {
            1 => (System.Windows.Media.Color.FromRgb(56, 189, 248),  0.8),  // cyan glow
            2 => (System.Windows.Media.Color.FromRgb(168, 85, 247),  0.8),  // violet glow
            3 => (System.Windows.Media.Color.FromRgb(74, 222, 128),  0.8),  // emerald mint glow
            4 => (System.Windows.Media.Color.FromRgb(251, 146, 60),  0.8),  // sunset amber glow
            _ => (System.Windows.Media.Color.FromRgb(255, 255, 255), 0.5)   // white glow
        };
        return new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Color = col, Opacity = op };
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        float peak = _peakMeter.GetPeak();
        _time += 0.25;

        // Use the full available height of the visualizer container
        double maxBarHeight = Math.Max(16.0, VisualizerContainer.ActualHeight - 2);

        if (peak > 0.005f)
        {
            LiveIndicator.Fill = _activeBrush;

            for (int i = 0; i < BarCount; i++)
            {
                // Dynamic wave flutter for energetic organic movement
                double flutter = 0.85 + 0.3 * Math.Sin(_time * 2.0 + i * 0.5) + (_rnd.NextDouble() - 0.5) * 0.2;
                double target = MinBarHeight + (maxBarHeight - MinBarHeight) * Math.Clamp(peak * _frequencyWeights[i] * flutter, 0.0, 1.0);

                if (target > _currentHeights[i])
                {
                    // Fast attack
                    _currentHeights[i] = target;
                }
                else
                {
                    // Smooth gravity falloff
                    _currentHeights[i] = Math.Max(MinBarHeight, _currentHeights[i] - 2.8);
                }

                _bars[i].Height = _currentHeights[i];
            }
        }
        else
        {
            LiveIndicator.Fill = _idleBrush;

            // Decay to resting line
            for (int i = 0; i < BarCount; i++)
            {
                if (_currentHeights[i] > MinBarHeight)
                {
                    _currentHeights[i] = Math.Max(MinBarHeight, _currentHeights[i] - 1.8);
                    _bars[i].Height = _currentHeights[i];
                }
            }
        }

        if (_settings.VisualizerStyle == 2)
        {
            UpdateWaveGeometry();
        }
    }

    private void UpdateWaveGeometry()
    {
        double width = Math.Max(50, VisualizerContainer.ActualWidth);
        double height = Math.Max(20, VisualizerContainer.ActualHeight);

        var figure = new PathFigure
        {
            StartPoint = new System.Windows.Point(0, height - _currentHeights[0]),
            IsClosed = false
        };

        double stepX = width / (BarCount - 1);
        for (int i = 1; i < BarCount; i++)
        {
            double x = i * stepX;
            double y = Math.Clamp(height - _currentHeights[i], 0, height - 2);
            figure.Segments.Add(new LineSegment(new System.Windows.Point(x, y), true));
        }

        var geom = new PathGeometry();
        geom.Figures.Add(figure);
        WavePath.Data = geom;
    }

    private async Task RefreshMediaInfoAsync()
    {
        var (title, artist, isPlaying) = await _mediaManager.GetCurrentMediaInfoAsync();

        if (!string.IsNullOrWhiteSpace(title))
        {
            TrackTitleText.Text = title;
            TrackArtistText.Text = !string.IsNullOrWhiteSpace(artist) ? artist : "Playing";
            PlayPauseBtn.Content = isPlaying ? "⏸" : "▶";
        }
        else
        {
            float peak = _peakMeter.GetPeak();
            if (peak > 0.02f)
            {
                TrackTitleText.Text = "System Audio";
                TrackArtistText.Text = "Playing";
                PlayPauseBtn.Content = "⏸";
            }
            else
            {
                TrackTitleText.Text = "No Media Playing";
                TrackArtistText.Text = "Windows Audio";
                PlayPauseBtn.Content = "▶";
            }
        }
    }

    private async void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
    {
        await _mediaManager.TogglePlayPauseAsync();
        await RefreshMediaInfoAsync();
    }

    private async void PrevBtn_Click(object sender, RoutedEventArgs e)
    {
        await _mediaManager.PreviousTrackAsync();
        await Task.Delay(300);
        await RefreshMediaInfoAsync();
    }

    private async void NextBtn_Click(object sender, RoutedEventArgs e)
    {
        await _mediaManager.NextTrackAsync();
        await Task.Delay(300);
        await RefreshMediaInfoAsync();
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void StyleBars_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerStyle = 0;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void StyleMirrored_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerStyle = 1;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void StyleWaveform_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerStyle = 2;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.MediaShowBackground = !_settings.MediaShowBackground;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        _settings.MediaShowTrackDetails = !_settings.MediaShowTrackDetails;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ColorWhite_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerColorMode = 0;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ColorCyan_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerColorMode = 1;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ColorViolet_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerColorMode = 2;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ColorMint_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerColorMode = 3;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void ColorAmber_Click(object sender, RoutedEventArgs e)
    {
        _settings.VisualizerColorMode = 4;
        RefreshVisualizerStyle();
        ScheduleDebouncedSave();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockState();
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
        app.WidgetService.RefreshLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, MainCardBorder, _settings.WidgetsLocked, hasResizeGrip: true);
        UpdateLockMenuHeader();
    }

    private void ResetSize_Click(object sender, RoutedEventArgs e)
    {
        Width = 300;
        Height = 130;
        _settings.MediaWidth = Width;
        _settings.MediaHeight = Height;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked
            ? "🔓  Unlock Position"
            : "🔒  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.MediaEnabled = false;
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
        Close();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _settings.MediaWidth = ActualWidth;
        _settings.MediaHeight = ActualHeight;
        ScheduleDebouncedSave();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.MediaX = Left;
        _settings.MediaY = Top;
        ScheduleDebouncedSave();
    }

    private void ScheduleDebouncedSave()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            var app = (App)Application.Current;
            _ = app.SettingsService.SaveAsync();
        };
        _saveDebounceTimer.Start();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _animTimer.Stop();
        _mediaTimer.Stop();
        _peakMeter.Dispose();

        _settings.MediaX = Left;
        _settings.MediaY = Top;
        _settings.MediaWidth = ActualWidth;
        _settings.MediaHeight = ActualHeight;

        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
