using System.Windows;
using System.Windows.Input;
using MorpheX.Core.Configuration;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class QuoteWidget : Window
{
    private readonly WidgetSettings _settings;

    private static readonly (string text, string author)[] Quotes =
    [
        ("Your time is limited, don't waste it living someone else's life.", "Steve Jobs"),
        ("The only way to do great work is to love what you do.", "Steve Jobs"),
        ("It does not matter how slowly you go as long as you do not stop.", "Confucius"),
        ("Our greatest glory is not in never falling, but in rising every time we fall.", "Confucius"),
        ("The future belongs to those who believe in the beauty of their dreams.", "Eleanor Roosevelt"),
        ("You miss 100% of the shots you don't take.", "Wayne Gretzky"),
        ("Whether you think you can or you think you can't, you're right.", "Henry Ford"),
        ("I have not failed. I've just found 10,000 ways that won't work.", "Thomas Edison"),
        ("Believe you can and you're halfway there.", "Theodore Roosevelt"),
        ("It always seems impossible until it's done.", "Nelson Mandela"),
        ("Life is what happens when you're busy making other plans.", "John Lennon"),
        ("In the middle of every difficulty lies opportunity.", "Albert Einstein"),
        ("The best time to plant a tree was 20 years ago. The second best time is now.", "Chinese Proverb"),
        ("An unexamined life is not worth living.", "Socrates"),
        ("Spread love everywhere you go.", "Mother Teresa"),
        ("Start where you are. Use what you have. Do what you can.", "Arthur Ashe"),
        ("Dream big and dare to fail.", "Norman Vaughan"),
        ("You only live once, but if you do it right, once is enough.", "Mae West"),
        ("The purpose of our lives is to be happy.", "Dalai Lama"),
        ("Get busy living or get busy dying.", "Stephen King"),
        ("You have brains in your head. You have feet in your shoes. You can steer yourself any direction you choose.", "Dr. Seuss"),
        ("If life were predictable it would cease to be life.", "Eleanor Roosevelt"),
        ("Spread your wings and let the fairy in you fly.", "Rumi"),
        ("Stay hungry, stay foolish.", "Steve Jobs"),
        ("The only impossible journey is the one you never begin.", "Tony Robbins"),
        ("In this life we cannot always do great things. But we can do small things with great love.", "Mother Teresa"),
        ("When you reach the end of your rope, tie a knot in it and hang on.", "Franklin D. Roosevelt"),
        ("Always remember that you are absolutely unique. Just like everyone else.", "Margaret Mead"),
        ("Do not go where the path may lead, go instead where there is no path and leave a trail.", "Ralph Waldo Emerson"),
        ("You will face many defeats in life, but never let yourself be defeated.", "Maya Angelou"),
    ];

    public QuoteWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.QuoteX;
        Top  = settings.QuoteY;

        ApplyTheme();
        UpdateLockState();
        ShowQuote(_settings.QuoteIndex);

        Closing += OnClosing;
    }

    public void RefreshDisplay() => ApplyTheme();

    public void ApplyTheme()
    {
        bool isLight = _settings.WidgetTheme == 1;
        WidgetStyles.ApplyFrostedGlass(CardBorder, _settings.QuoteShowBackground, cornerRadius: 12, isLightTheme: isLight);

        QuoteText.FontSize = _settings.QuoteFontSize switch { 0 => 11, 2 => 16, _ => 13 };
        AuthorText.FontSize = _settings.QuoteFontSize switch { 0 => 10, 2 => 13, _ => 11 };

        QuoteText.FontFamily = _settings.QuoteFontMode switch
        {
            1 => new System.Windows.Media.FontFamily("Segoe UI Light, Segoe UI"),
            2 => new System.Windows.Media.FontFamily("Bahnschrift Light, Segoe UI"),
            _ => new System.Windows.Media.FontFamily("Georgia, Times New Roman")
        };

        var (textBrush, markBrush, authorBrush) = _settings.QuoteColorMode switch
        {
            1 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 56, 189, 248)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 56, 189, 248))),
            2 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 74, 222, 128)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 74, 222, 128))),
            3 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 192, 132, 252)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 192, 132, 252))),
            4 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 251, 146, 60)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 251, 146, 60))),
            5 => (new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 251, 113, 133)),
                  new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 251, 113, 133))),
            _ => (WidgetStyles.GetWidgetForeground(isLight),
                  isLight ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0x22, 0x1E, 0x18))
                          : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                  WidgetStyles.GetWidgetSubtleForeground(isLight))
        };

        QuoteMarkText.Foreground = markBrush;
        QuoteText.Foreground = textBrush;
        AuthorText.Foreground = authorBrush;

        Opacity = Math.Clamp(_settings.QuoteOpacity > 0 ? _settings.QuoteOpacity : 1.0, 0.2, 1.0);
        MorpheX.Services.WidgetWindowHelper.RefreshWidgetSurface(this);

        if (ToggleCardMenu != null)
            ToggleCardMenu.Header = _settings.QuoteShowBackground ? "Hide Background Card" : "Show Background Card";
    }

    private void ShowQuote(int index)
    {
        index = ((index % Quotes.Length) + Quotes.Length) % Quotes.Length;
        _settings.QuoteIndex = index;
        QuoteText.Text  = Quotes[index].text;
        AuthorText.Text = "- " + Quotes[index].author;
    }

    private void NextQuote_Click(object sender, RoutedEventArgs e)
    {
        ShowQuote(_settings.QuoteIndex + 1);
        Save();
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        if (e.ClickCount == 2) { ShowQuote(_settings.QuoteIndex + 1); Save(); return; }
        DragMove();
        Save();
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
        Left = _settings.QuoteX;
        Top = _settings.QuoteY;
        ApplyTheme();
        UpdateLockState();
    }

    public void UpdateLockState()
    {
        WidgetStyles.ApplyLockState(this, CardBorder, _settings.WidgetsLocked);
        LockMenuItem.Header = _settings.WidgetsLocked ? "Unlock Position" : "Lock Position";
    }

    private void ToggleCard_Click(object sender, RoutedEventArgs e)
    {
        _settings.QuoteShowBackground = !_settings.QuoteShowBackground;
        ApplyTheme();
        Save();
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && int.TryParse(item.Tag?.ToString(), out int c))
        {
            _settings.QuoteColorMode = c;
            ApplyTheme();
            Save();
        }
    }

    private void SizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && int.TryParse(item.Tag?.ToString(), out int sz))
        {
            _settings.QuoteFontSize = sz;
            ApplyTheme();
            Save();
        }
    }

    private void FontPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && int.TryParse(item.Tag?.ToString(), out int f))
        {
            _settings.QuoteFontMode = f;
            ApplyTheme();
            Save();
        }
    }

    private void Opacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && double.TryParse(item.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double op))
        {
            _settings.QuoteOpacity = op;
            ApplyTheme();
            Save();
        }
    }

    private void ReloadWidget_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        app?.WidgetService.ReloadWidget(typeof(QuoteWidget));
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.QuoteEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.QuoteX = Left;
        _settings.QuoteY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.QuoteX = Left;
        _settings.QuoteY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
