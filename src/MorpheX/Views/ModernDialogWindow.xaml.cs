using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;
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

        WindowTitleText.Text = title;
        HeadingText.Text = heading;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;

        if (string.IsNullOrWhiteSpace(secondaryText))
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SecondaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Content = secondaryText;
        }

        ApplyIconStyle(icon);
    }

    private static SolidColorBrush Brush(string hex) =>
        new((WpfColor)WpfColorConverter.ConvertFromString(hex));

    private void ApplyIconStyle(ModernDialogIcon icon)
    {
        switch (icon)
        {
            case ModernDialogIcon.Info:
                DialogSymbolIcon.Symbol = SymbolRegular.Info24;
                DialogSymbolIcon.Foreground = Brush("#60A5FA");
                IconContainer.Background = Brush("#172554");
                IconContainer.BorderBrush = Brush("#1E3A8A");
                break;

            case ModernDialogIcon.Warning:
                DialogSymbolIcon.Symbol = SymbolRegular.Warning24;
                DialogSymbolIcon.Foreground = Brush("#FBBF24");
                IconContainer.Background = Brush("#35260C");
                IconContainer.BorderBrush = Brush("#5C4113");
                break;

            case ModernDialogIcon.Error:
                DialogSymbolIcon.Symbol = SymbolRegular.DismissCircle24;
                DialogSymbolIcon.Foreground = Brush("#F87171");
                IconContainer.Background = Brush("#3B1318");
                IconContainer.BorderBrush = Brush("#641D26");
                break;

            case ModernDialogIcon.Question:
                DialogSymbolIcon.Symbol = SymbolRegular.QuestionCircle24;
                DialogSymbolIcon.Foreground = Brush("#A78BFA");
                IconContainer.Background = Brush("#261C4C");
                IconContainer.BorderBrush = Brush("#433285");
                break;

            case ModernDialogIcon.Success:
                DialogSymbolIcon.Symbol = SymbolRegular.CheckmarkCircle24;
                DialogSymbolIcon.Foreground = Brush("#34D399");
                IconContainer.Background = Brush("#0C2B1D");
                IconContainer.BorderBrush = Brush("#165337");
                break;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
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

    // ─── Static Convenience API ──────────────────────────────────────────────

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
