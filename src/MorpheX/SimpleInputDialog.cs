using Window = System.Windows.Window;
using Application = System.Windows.Application;
using SizeToContent = System.Windows.SizeToContent;
using ResizeMode = System.Windows.ResizeMode;
using WindowStartupLocation = System.Windows.WindowStartupLocation;
using Thickness = System.Windows.Thickness;
using TextWrapping = System.Windows.TextWrapping;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace MorpheX;

/// <summary>Small reusable text prompt for library organization actions.</summary>
public static class SimpleInputDialog
{
    public static string? Show(string title, string prompt, string initialValue = "")
    {
        var owner = Application.Current?.MainWindow;
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            Foreground = Brushes.White,
            Padding = new Thickness(22)
        };

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = prompt,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var input = new TextBox
        {
            Text = initialValue,
            MinWidth = 360,
            Padding = new Thickness(8, 6, 8, 6)
        };
        panel.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", MinWidth = 78, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        var confirm = new Button { Content = "Save", MinWidth = 78, IsDefault = true };
        confirm.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }
}
