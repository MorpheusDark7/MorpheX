using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfApplication = System.Windows.Application;

namespace MorpheX.Views;

public enum ModernDialogIcon
{
    Info,
    Warning,
    Error,
    Question,
    Success
}

public partial class ModernDialogWindow : Window
{
    public bool Result { get; private set; }

    public ModernDialogWindow(
        string title,
        string heading,
        string message,
        string primaryText = "OK",
        string? secondaryText = null,
        ModernDialogIcon icon = ModernDialogIcon.Info,
        Window? owner = null)
    {
        InitializeComponent();

        if (owner != null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else if (WpfApplication.Current?.MainWindow != null && WpfApplication.Current.MainWindow.IsVisible)
        {
            Owner = WpfApplication.Current.MainWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        WindowTitleText.Text  = title;
        HeadingText.Text      = heading;
        MessageText.Text      = message;
        PrimaryButton.Content = primaryText;

        if (string.IsNullOrWhiteSpace(secondaryText))
            SecondaryButton.Visibility = Visibility.Collapsed;
        else
        {
            SecondaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Content    = secondaryText;
        }

        ApplyIconStyle(icon);
    }

    private static SolidColorBrush Brush(string hex) =>
        new((WpfColor)WpfColorConverter.ConvertFromString(hex));

    private void ApplyIconStyle(ModernDialogIcon icon)
    {
        // Colour the heading and primary button to match the severity — no AI badge/icon needed
        switch (icon)
        {
            case ModernDialogIcon.Info:
                HeadingText.Foreground   = Brush("#C0CCFF");
                PrimaryButton.Background = Brush("#2952CC");
                break;

            case ModernDialogIcon.Warning:
                HeadingText.Foreground   = Brush("#FBBF24");
                PrimaryButton.Background = Brush("#7C4B00");
                break;

            case ModernDialogIcon.Error:
                HeadingText.Foreground   = Brush("#F87171");
                PrimaryButton.Background = Brush("#8B1A1A");
                break;

            case ModernDialogIcon.Question:
                HeadingText.Foreground   = Brush("#C4B5FD");
                PrimaryButton.Background = Brush("#3B3BCC");
                break;

            case ModernDialogIcon.Success:
                HeadingText.Foreground   = Brush("#34D399");
                PrimaryButton.Background = Brush("#065F46");
                break;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Result = true;
            DialogResult = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Result = false;
            DialogResult = false;
            Close();
        }
    }

    // ─── Static Convenience API ──────────────────────────────────────────────

    public static string? Prompt(
        string title,
        string heading,
        string message,
        string defaultText = "",
        string primaryText = "Save",
        string secondaryText = "Cancel",
        ModernDialogIcon icon = ModernDialogIcon.Info,
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, primaryText, secondaryText, icon, owner);
        dlg.InputTextBox.Visibility = Visibility.Visible;
        dlg.InputTextBox.Text = defaultText;
        dlg.Loaded += (_, _) =>
        {
            dlg.InputTextBox.Focus();
            dlg.InputTextBox.SelectAll();
        };
        dlg.ShowDialog();
        return dlg.Result ? dlg.InputTextBox.Text.Trim() : null;
    }

    public static bool Confirm(
        string title,
        string heading,
        string message,
        string primaryText = "Yes",
        string secondaryText = "No",
        ModernDialogIcon icon = ModernDialogIcon.Question,
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, primaryText, secondaryText, icon, owner);
        dlg.ShowDialog();
        return dlg.Result;
    }

    public static void Information(
        string title,
        string heading,
        string message,
        string buttonText = "OK",
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, buttonText, null, ModernDialogIcon.Info, owner);
        dlg.ShowDialog();
    }

    public static void Warning(
        string title,
        string heading,
        string message,
        string buttonText = "OK",
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, buttonText, null, ModernDialogIcon.Warning, owner);
        dlg.ShowDialog();
    }

    public static void Error(
        string title,
        string heading,
        string message,
        string buttonText = "OK",
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, buttonText, null, ModernDialogIcon.Error, owner);
        dlg.ShowDialog();
    }

    public static void Success(
        string title,
        string heading,
        string message,
        string buttonText = "OK",
        Window? owner = null)
    {
        var dlg = new ModernDialogWindow(title, heading, message, buttonText, null, ModernDialogIcon.Success, owner);
        dlg.ShowDialog();
    }
}
