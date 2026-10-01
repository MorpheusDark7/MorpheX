using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using MorpheX.Services;
using Application = System.Windows.Application;

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

        // Precompute frequency weights across bars (slight curve emphasizing bass/mids)
        for (int i = 0; i < BarCount; i++)
        {
            double norm = (double)i / (BarCount - 1);
            // Bell-like curve with slight bass bias
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
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Border
            {
                Margin = new Thickness(1, 0, 1, 0),
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 235, 235, 240)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = MinBarHeight
            };
            _bars[i] = bar;
            BarsGrid.Children.Add(bar);
        }

        _animTimer.Start();
        _mediaTimer.Start();

        await _mediaManager.InitializeAsync();
        await RefreshMediaInfoAsync();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        float peak = _peakMeter.GetPeak();
        _time += 0.25;

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
        DragMove();
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
