using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX;

public static class MonitorPickerHelper
{
    public static async Task ApplyWallpaperWithPickerAsync(WallpaperInfo wallpaper, Action? onComplete = null)
    {
        var app = (App)Application.Current;
        var monitors = app.MonitorService.Monitors;

        if (monitors.Count == 0)
        {
            Log.Warning("No monitors found, refreshing display list");
            app.MonitorService.Refresh();
            monitors = app.MonitorService.Monitors;
        }

        if (monitors.Count == 0)
        {
            Log.Error("Cannot apply wallpaper: No monitors available");
            return;
        }

        if (monitors.Count == 1)
        {
            await ApplyToMonitorAsync(monitors[0].DeviceId, wallpaper);
            onComplete?.Invoke();
            return;
        }

        ShowMonitorPicker(monitors, wallpaper, onComplete);
    }

    public static async Task ApplyToMonitorAsync(string monitorDeviceId, WallpaperInfo wallpaper)
    {
        var app = (App)Application.Current;
        try
        {
            await app.WallpaperService.SetWallpaperAsync(monitorDeviceId, wallpaper);
            Log.Information("Applied wallpaper '{Name}' on monitor {DeviceId}",
                wallpaper.Name, monitorDeviceId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply wallpaper '{Name}' on monitor {DeviceId}",
                wallpaper.Name, monitorDeviceId);
            System.Windows.MessageBox.Show(
                $"Could not apply wallpaper \"{wallpaper.Name}\":\n\n{ex.Message}",
                "Wallpaper Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void ShowMonitorPicker(IReadOnlyList<MonitorInfo> monitors,
                                          WallpaperInfo wallpaper,
                                          Action? onComplete)
    {
        var mainWindow = Application.Current.MainWindow;

        var dialog = new Window
        {
            Title = "Select Monitor",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = mainWindow,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent
        };

        var outerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(240, 32, 32, 32)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 4,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };

        var mainStack = new StackPanel();

        var titleText = new TextBlock
        {
            Text = "Apply Wallpaper To",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        mainStack.Children.Add(titleText);

        var subtitleText = new TextBlock
        {
            Text = $"\"{wallpaper.Name}\"",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            Margin = new Thickness(0, 0, 0, 16),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        mainStack.Children.Add(subtitleText);

        foreach (var monitor in monitors)
        {
            var btn = CreateMonitorButton(
                monitor.DisplayName,
                $"{monitor.Bounds.Width}×{monitor.Bounds.Height} @ {monitor.RefreshRate}Hz" +
                    (monitor.IsPrimary ? "  •  Primary" : ""),
                monitor.IsPrimary ? Color.FromRgb(0, 120, 212) : Color.FromRgb(60, 60, 60));

            var capturedMonitor = monitor;
            btn.Click += async (_, _) =>
            {
                dialog.Close();
                await ApplyToMonitorAsync(capturedMonitor.DeviceId, wallpaper);
                onComplete?.Invoke();
            };

            mainStack.Children.Add(btn);
        }

        var sep = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Margin = new Thickness(0, 8, 0, 8)
        };
        mainStack.Children.Add(sep);

        var allBtn = CreateMonitorButton(
            "All Monitors",
            $"Apply to all {monitors.Count} displays",
            Color.FromRgb(16, 124, 65));

        allBtn.Click += async (_, _) =>
        {
            dialog.Close();
            var app = (App)Application.Current;
            try
            {
                await app.WallpaperService.SetWallpaperOnAllMonitorsAsync(wallpaper);
                Log.Information("Applied wallpaper '{Name}' on all monitors", wallpaper.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply wallpaper '{Name}' on all monitors", wallpaper.Name);
            }
            onComplete?.Invoke();
        };
        mainStack.Children.Add(allBtn);

        var cancelBtn = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(0, 8, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        cancelBtn.Click += (_, _) => dialog.Close();
        mainStack.Children.Add(cancelBtn);

        outerBorder.Child = mainStack;
        dialog.Content = outerBorder;

        dialog.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Escape)
                dialog.Close();
        };

        dialog.ShowDialog();
    }

    private static Button CreateMonitorButton(string title, string subtitle, Color accentColor)
    {
        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12, 10, 12, 10),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(accentColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconText = new TextBlock
        {
            Text = "🖥",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        };
        iconBorder.Child = iconText;
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White
        });
        textStack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        btn.Content = grid;

        btn.MouseEnter += (_, _) =>
            btn.Background = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        btn.MouseLeave += (_, _) =>
            btn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));

        return btn;
    }
}
