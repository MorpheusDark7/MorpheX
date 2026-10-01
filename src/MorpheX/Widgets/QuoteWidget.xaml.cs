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

        UpdateLockMenuHeader();
        ShowQuote(_settings.QuoteIndex);

        Closing += OnClosing;
    }

    private void ShowQuote(int index)
    {
        index = ((index % Quotes.Length) + Quotes.Length) % Quotes.Length;
        _settings.QuoteIndex = index;
        QuoteText.Text  = Quotes[index].text;
        AuthorText.Text = "— " + Quotes[index].author;
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
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        Save();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked ? "🔓  Unlock Position" : "🔒  Lock Position";
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
