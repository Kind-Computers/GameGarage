// Copyright Kind Computers. Licensed under the MIT License.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GameGarage.Interface;

/// <summary>
/// A brief, decorative effect on a dedicated overlay. All calls belong to the UI thread;
/// playback never waits for animation completion or controls the verification task.
/// </summary>
internal sealed class VerificationEffects : IDisposable
{
    private static readonly TimeSpan EffectDuration = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan ParticleDuration = TimeSpan.FromMilliseconds(620);
    private static readonly SolidColorBrush[] Colors =
    [
        FrozenBrush(0xFF, 0xD2, 0x78),
        FrozenBrush(0xFF, 0x89, 0x6D),
        FrozenBrush(0xFF, 0xF1, 0xC8)
    ];

    // Ten deterministic particles keep the effect small and fixture captures reproducible.
    private static readonly (double X, double Y, double Dx, double Dy, double Size)[] Particles =
    [
        (-.46, -.30, -30, -22, 4),
        (-.32, -.50, -22, -34, 6),
        (-.10, -.52,  -8, -42, 3),
        ( .18, -.52,  12, -36, 5),
        ( .44, -.28,  32, -24, 4),
        ( .47,  .24,  38,  10, 6),
        ( .30,  .49,  24,  28, 3),
        ( .03,  .51,   2,  34, 5),
        (-.25,  .48, -18,  28, 4),
        (-.46,  .24, -34,  10, 3)
    ];

    private readonly Canvas overlay;
    private readonly FrameworkElement button;
    private Storyboard? storyboard;
    private EventHandler? completed;
    private bool disposed;

    internal VerificationEffects(Canvas overlay, FrameworkElement button)
    {
        this.overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        this.button = button ?? throw new ArgumentNullException(nameof(button));
        MakeNoninteractive();
    }

    internal bool IsActive => storyboard != null;

    internal void Play(bool animationsAllowed)
    {
        Stop();
        if (disposed || !animationsAllowed || button.ActualWidth <= 0 || button.ActualHeight <= 0)
            return;

        Point center;
        try
        {
            center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), overlay);
        }
        catch (InvalidOperationException)
        {
            // The view may be unloading or the fixture may not have joined the visual tree yet.
            return;
        }
        catch (ArgumentException)
        {
            return;
        }
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y))
            return;

        var next = new Storyboard { Duration = EffectDuration, FillBehavior = FillBehavior.Stop };
        AddFrame(next, center);

        for (int i = 0; i < Particles.Length; i++)
        {
            var particle = Particles[i];
            var translate = new TranslateTransform();
            var square = new Rectangle
            {
                Width = particle.Size,
                Height = particle.Size,
                Fill = Colors[i % Colors.Length],
                Opacity = 0,
                IsHitTestVisible = false,
                Focusable = false,
                SnapsToDevicePixels = true,
                RenderTransform = translate
            };
            RenderOptions.SetEdgeMode(square, EdgeMode.Aliased);
            Canvas.SetLeft(square, center.X + button.ActualWidth * particle.X - particle.Size / 2);
            Canvas.SetTop(square, center.Y + button.ActualHeight * particle.Y - particle.Size / 2);
            overlay.Children.Add(square);

            TimeSpan delay = TimeSpan.FromMilliseconds(i % 3 * 15);
            AddMotion(next, square, TranslateTransform.XProperty, 0, particle.Dx, ParticleDuration, delay);
            AddMotion(next, square, TranslateTransform.YProperty, 0, particle.Dy, ParticleDuration, delay);
            var opacity = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = delay,
                Duration = ParticleDuration,
                FillBehavior = FillBehavior.Stop
            };
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(45))));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190))));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(ParticleDuration)));
            Target(next, opacity, square, UIElement.OpacityProperty);
        }

        storyboard = next;
        completed = (_, _) =>
        {
            // An already queued completion from a replaced playback must not stop a new one.
            if (ReferenceEquals(storyboard, next))
                Stop();
        };
        next.Completed += completed;
        try
        {
            next.Begin(overlay, HandoffBehavior.SnapshotAndReplace, isControllable: true);
        }
        catch (InvalidOperationException)
        {
            // A decorative playback failure must not prevent verification from starting.
            Stop();
        }
    }

    /// <summary>Fixture hook: freeze playback at a bounded sample time for rendering.</summary>
    internal void Seek(TimeSpan position)
    {
        if (storyboard == null)
            return;
        if (position >= EffectDuration)
        {
            Stop();
            return;
        }
        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;
        storyboard.Pause(overlay);
        storyboard.SeekAlignedToLastTick(overlay, position, TimeSeekOrigin.BeginTime);
    }

    internal void Stop()
    {
        MakeNoninteractive();
        Storyboard? previous = storyboard;
        EventHandler? previousCompleted = completed;
        storyboard = null;
        completed = null;
        if (previous != null)
        {
            if (previousCompleted != null)
                previous.Completed -= previousCompleted;
            try
            {
                previous.Remove(overlay);
            }
            catch (InvalidOperationException)
            {
                // There may be no controllable clock if Begin failed while unloading.
            }
        }
        overlay.Children.Clear();
    }

    public void Dispose()
    {
        disposed = true;
        Stop();
    }

    private void AddFrame(Storyboard animation, Point center)
    {
        var scale = new ScaleTransform(1, 1);
        var frame = new Rectangle
        {
            Width = button.ActualWidth + 10,
            Height = button.ActualHeight + 10,
            Stroke = Colors[0],
            StrokeThickness = 2,
            Opacity = 0,
            IsHitTestVisible = false,
            Focusable = false,
            SnapsToDevicePixels = true,
            RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = scale
        };
        RenderOptions.SetEdgeMode(frame, EdgeMode.Aliased);
        Canvas.SetLeft(frame, center.X - frame.Width / 2);
        Canvas.SetTop(frame, center.Y - frame.Height / 2);
        overlay.Children.Add(frame);
        AddMotion(animation, frame, ScaleTransform.ScaleXProperty, 1, 1.18, EffectDuration, TimeSpan.Zero);
        AddMotion(animation, frame, ScaleTransform.ScaleYProperty, 1, 1.30, EffectDuration, TimeSpan.Zero);
        var opacity = new DoubleAnimation(.75, 0, EffectDuration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Target(animation, opacity, frame, UIElement.OpacityProperty);
    }

    private static void AddMotion(Storyboard storyboard, DependencyObject target, DependencyProperty property,
        double from, double to, TimeSpan duration, TimeSpan delay)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            BeginTime = delay,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, property));
        storyboard.Children.Add(animation);
    }

    private static void Target(Storyboard storyboard, AnimationTimeline animation,
        DependencyObject target, DependencyProperty property)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(property));
        storyboard.Children.Add(animation);
    }

    private void MakeNoninteractive()
    {
        overlay.IsHitTestVisible = false;
        overlay.Focusable = false;
    }

    private static SolidColorBrush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
