using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MorpheX.Core.Desktop;
using MorpheX.Core.Models;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MorpheX;

public static class DisplayIdentifyHelper
{
    private static readonly List<Window> _activeOverlays = new();

    public static void ShowIdentifyOverlays()
    {
        CloseExisting();

        var app = (App)Application.Current;
        var monitors = app.MonitorService.Monitors;

        if (monitors.Count == 0)
        {
            app.MonitorService.Refresh();
            monitors = app.MonitorService.Monitors;
        }

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            int displayIndex = i + 1;

            var window = CreateIdentifyWindow(monitor, displayIndex);
            _activeOverlays.Add(window);
            window.Show();
        }
    }

    private static void CloseExisting()
    {
        foreach (var win in _activeOverlays)
        {
            try { win.Close(); } catch { }
        }
        _activeOverlays.Clear();
    }

    private static Window CreateIdentifyWindow(MonitorInfo monitor, int index)
    {
        const double cardSize = 220;

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Width = cardSize,
            Height = cardSize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize,
            Cursor = Cursors.Hand
        };

        var border = new Border
        {
            Width = cardSize,
            Height = cardSize,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(235, 24, 24, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 6,
                Opacity = 0.6,
                Color = Colors.Black
            }
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var numberText = new TextBlock
        {
            Text = index.ToString(),
            FontSize = 88,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -8, 0, 0)
        };
        stack.Children.Add(numberText);

        var nameText = new TextBlock
        {
            Text = monitor.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 190
        };
        stack.Children.Add(nameText);

        var specsText = new TextBlock
        {
            Text = $"{monitor.Bounds.Width}×{monitor.Bounds.Height}" + (monitor.IsPrimary ? "  •  Primary" : ""),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        stack.Children.Add(specsText);

        border.Child = stack;
        window.Content = border;

        window.MouseDown += (_, _) => CloseWindowWithFade(window);
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                CloseWindowWithFade(window);
        };

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            int screenX = monitor.Bounds.X + (monitor.Bounds.Width - (int)(cardSize * monitor.DpiScale)) / 2;
            int screenY = monitor.Bounds.Y + (monitor.Bounds.Height - (int)(cardSize * monitor.DpiScale)) / 2;
            int screenW = (int)(cardSize * monitor.DpiScale);
            int screenH = (int)(cardSize * monitor.DpiScale);

            NativeMethods.SetWindowPos(hwnd, new IntPtr(-1) ,
                screenX, screenY, screenW, screenH,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CloseWindowWithFade(window);
        };
        timer.Start();

        return window;
    }

    private static void CloseWindowWithFade(Window window)
    {
        try
        {
            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
            fade.Completed += (_, _) =>
            {
                try
                {
                    window.Close();
                    _activeOverlays.Remove(window);
                }
                catch { }
            };
            window.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        catch
        {
            try
            {
                window.Close();
                _activeOverlays.Remove(window);
            }
            catch { }
        }
    }
}
