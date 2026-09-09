using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MorpheX;

public partial class SplashWindow : Window
{
    private Storyboard? _arcStoryboard;
    private bool _reduced;

    public SplashWindow()
    {
        InitializeComponent();

        _reduced = SystemParameters.ClientAreaAnimation == false;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlayEntranceSequence();
    }

    private void PlayEntranceSequence()
    {
        var sb = new Storyboard();

        void AddDouble(DependencyObject target, string prop, double from, double to,
                       double beginSec, double durationSec, IEasingFunction? ease = null)
        {
            var anim = new DoubleAnimation
            {
                From = from, To = to,
                BeginTime = TimeSpan.FromSeconds(beginSec),
                Duration = new Duration(TimeSpan.FromSeconds(durationSec)),
                EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath(prop));
            sb.Children.Add(anim);
        }

        AddDouble(RootBorder, "Opacity", 0, 1, 0, 0.25);

        if (!_reduced)
        {
            AddDouble(BrandIcon, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)", 0.85, 1.0, 0.1, 0.55);
            AddDouble(BrandIcon, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)", 0.85, 1.0, 0.1, 0.55);
        }
        else
        {
            IconScale.ScaleX = 1;
            IconScale.ScaleY = 1;
        }

        AddDouble(BrandIcon, "Opacity", 0, 1, 0.1, 0.45);
        AddDouble(TitleText, "Opacity", 0, 1, 0.45, 0.35);
        AddDouble(TaglineText, "Opacity", 0, 0.65, 0.60, 0.35);
        AddDouble(StatusText, "Opacity", 0, 1, 0.70, 0.3);
        AddDouble(TrackBorder, "Opacity", 0, 1, 0.70, 0.3);

        if (!_reduced)
        {
            AddDouble(Arc1, "Opacity", 0, 1, 0.5, 0.5);
            AddDouble(Arc2, "Opacity", 0, 0.7, 0.65, 0.5);
        }

        sb.Begin(this);

        if (!_reduced)
        {
            StartArcRotation();
        }
    }

    private void StartArcRotation()
    {
        _arcStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        var arc1Anim = new DoubleAnimation
        {
            From = 0, To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(28)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(arc1Anim, Arc1Rotate);
        Storyboard.SetTargetProperty(arc1Anim, new PropertyPath(System.Windows.Media.RotateTransform.AngleProperty));
        _arcStoryboard.Children.Add(arc1Anim);

        var arc2Anim = new DoubleAnimation
        {
            From = 360, To = 0,
            Duration = new Duration(TimeSpan.FromSeconds(20)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(arc2Anim, Arc2Rotate);
        Storyboard.SetTargetProperty(arc2Anim, new PropertyPath(System.Windows.Media.RotateTransform.AngleProperty));
        _arcStoryboard.Children.Add(arc2Anim);

        _arcStoryboard.Begin(this);
    }

    public void SetStatus(string message)
    {
        Dispatcher.InvokeAsync(() => StatusText.Text = message);
    }

    public void SetProgress(double fraction)
    {
        Dispatcher.InvokeAsync(() =>
        {
            double trackWidth = TrackBorder.ActualWidth;
            if (trackWidth > 0)
            {
                ProgressFill.Width = trackWidth * Math.Clamp(fraction, 0, 1);
            }
        });
    }

    public Task CloseWithFadeAsync()
    {
        var tcs = new TaskCompletionSource();

        Dispatcher.Invoke(() =>
        {
            _arcStoryboard?.Stop(this);
            _arcStoryboard = null;

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(320)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                tcs.TrySetResult();
            };

            RootBorder.BeginAnimation(OpacityProperty, fadeOut);
        });

        return tcs.Task;
    }
}
