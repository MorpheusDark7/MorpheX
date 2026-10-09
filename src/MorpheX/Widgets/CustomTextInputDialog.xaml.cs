using System.Windows;
using System.Windows.Input;

namespace MorpheX.Widgets;

public partial class CustomTextInputDialog : Window
{
    public string EnteredText { get; private set; } = string.Empty;

    public CustomTextInputDialog(string currentText)
    {
        InitializeComponent();
        InputBox.Text = currentText;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        int len = InputBox.Text.Length;
        CharCounter.Text = len + " / 20";
        CharCounter.Foreground = len >= 18
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 96, 128));
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; Ok_Click(sender, e); }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        EnteredText = InputBox.Text.Trim();
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
