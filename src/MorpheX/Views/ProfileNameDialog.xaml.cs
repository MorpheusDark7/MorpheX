using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MorpheX.Views;

public partial class ProfileNameDialog : Window
{
    public string ProfileName { get; private set; } = string.Empty;
    public bool IncludeWallpaper => IncludeWallpaperCheck.IsChecked == true;

    public ProfileNameDialog(string currentName = "", string title = "Desktop Profile", string heading = "Name your profile", bool showWallpaperCheck = true)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        HeadingLabel.Text = heading;
        InputBox.Text = currentName;
        InputBox.SelectAll();

        IncludeWallpaperCheck.Visibility = showWallpaperCheck ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => InputBox.Focus();
    }

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        int len = InputBox.Text.Length;
        CharCounter.Text = $"{len} / 28";
        CharCounter.Foreground = len >= 26
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 96, 128));
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            Ok_Click(sender, e);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Profile " + DateTime.Now.ToString("MMM d");
        }
        ProfileName = text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
