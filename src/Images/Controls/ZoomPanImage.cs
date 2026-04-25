using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Controls;

/// <summary>
/// A content host that draws its image with wheel-zoom, drag-pan, and double-click fit/1:1 toggle.
/// Used as the photo canvas. When the <see cref="Animation"/> DP is set, the inner Image cycles
/// through the frames of the supplied <see cref="AnimationSequence"/> via WPF's keyframe animator —
/// all pan/zoom/rotate transforms stay intact on top of the moving image.
/// </summary>
public sealed class ZoomPanImage : ContentControl
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    // Flip lives BEFORE rotate in the transform stack so a horizontal flip flips the image in
    // its own canvas frame rather than the post-rotation frame — that's what users expect.
    // Order: flip → rotate → zoom-scale → pan-translate (TransformGroup applies children in order).
    private readonly ScaleTransform _flip = new(1, 1);
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly RotateTransform _rotate = new(0);
    private readonly Grid _root = new();
    private Point? _dragStart;
    private Point _dragOrigin;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(ZoomPanImage),
        new PropertyMetadata(null, (d, e) => ((ZoomPanImage)d).OnSourceChanged((ImageSource?)e.NewValue)));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty RotationProperty = DependencyProperty.Register(
        nameof(Rotation), typeof(double), typeof(ZoomPanImage),
        new PropertyMetadata(0.0, (d, e) => ((ZoomPanImage)d).OnRotationChanged((double)e.NewValue)));

    public double Rotation
    {
        get => (double)GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public static readonly DependencyProperty AnimationProperty = DependencyProperty.Register(
        nameof(Animation), typeof(AnimationSequence), typeof(ZoomPanImage),
        new PropertyMetadata(null, (d, e) => ((ZoomPanImage)d).OnAnimationChanged((AnimationSequence?)e.NewValue)));

    /// <summary>
    /// Multi-frame animation played on top of <see cref="Source"/>. Null means static image.
    /// </summary>
    public AnimationSequence? Animation
    {
        get => (AnimationSequence?)GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    private static readonly CubicEase _rotateEase = new() { EasingMode = EasingMode.EaseInOut };

    private void OnRotationChanged(double target)
    {
        // 90-degree flips snap in the source model; animating the RotateTransform gives the
        // viewer a premium spin-to-new-orientation feel instead of a frame-one teleport.
        // Duration scales with angular delta so large jumps (e.g. 0 -> 270) still feel controlled.
        var delta = Math.Abs(target - _rotate.Angle);
        var duration = TimeSpan.FromMilliseconds(180 + Math.Min(delta, 180) * 0.9);
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = duration,
            EasingFunction = _rotateEase,
            FillBehavior = FillBehavior.HoldEnd,
        };
        _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    public static readonly DependencyProperty FlipHorizontalProperty = DependencyProperty.Register(
        nameof(FlipHorizontal), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d)._flip.ScaleX = (bool)e.NewValue ? -1 : 1));

    public bool FlipHorizontal
    {
        get => (bool)GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    public static readonly DependencyProperty FlipVerticalProperty = DependencyProperty.Register(
        nameof(FlipVertical), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d)._flip.ScaleY = (bool)e.NewValue ? -1 : 1));

    public bool FlipVertical
    {
        get => (bool)GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    public ZoomPanImage()
    {
        var group = new TransformGroup();
        group.Children.Add(_flip);
        group.Children.Add(_rotate);
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        _image.RenderTransformOrigin = new Point(0.5, 0.5);
        _image.RenderTransform = group;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

        _root.ClipToBounds = true;
        _root.Children.Add(_image);
        Content = _root;

        MouseWheel += OnWheel;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseDoubleClick += OnDouble;
        SizeChanged += (_, _) => ResetView();
    }

    private void OnSourceChanged(ImageSource? src)
    {
        // A new source always wins over whatever animation was playing. The Animation DP will
        // re-apply its keyframes in its own PropertyChanged callback; we rely on the view model
        // to raise CurrentImage *before* CurrentAnimation so the final state is "new image,
        // new animation" rather than "new image, stale frames".
        _image.BeginAnimation(Image.SourceProperty, null);
        _image.Source = src;
        ResetView();
    }

    private void OnAnimationChanged(AnimationSequence? seq)
    {
        // Cancel whatever was playing before — either starts a fresh keyframe animation or
        // reverts the Image.Source to its baseline (which OnSourceChanged set moments ago).
        _image.BeginAnimation(Image.SourceProperty, null);
        if (seq is null || seq.Frames.Count < 2) return;

        var anim = new ObjectAnimationUsingKeyFrames();
        var t = TimeSpan.Zero;
        for (int i = 0; i < seq.Frames.Count; i++)
        {
            anim.KeyFrames.Add(new DiscreteObjectKeyFrame(seq.Frames[i], KeyTime.FromTimeSpan(t)));
            t += seq.Delays[i];
        }
        anim.Duration = t;
        anim.RepeatBehavior = seq.LoopCount <= 0
            ? RepeatBehavior.Forever
            : new RepeatBehavior(seq.LoopCount);

        // HandoffBehavior.SnapshotAndReplace ensures we don't blend with a lingering previous
        // animation's interpolator (discrete keyframes wouldn't blend anyway, but it's the
        // conservative default for source swaps).
        _image.BeginAnimation(Image.SourceProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    public void ResetView()
    {
        _scale.ScaleX = _scale.ScaleY = 1;
        _translate.X = _translate.Y = 0;
    }

    // V20-20: four zoom modes. All compute against the source image's pixel size in the
    // control's current available size. Stretch.Uniform on the inner Image handles the baseline
    // fit; our ScaleTransform multiplies on top. Fit = 1.0x (baseline Uniform is already fit);
    // 1:1 = inverse of Uniform's baseline fit so each source pixel maps to one device pixel;
    // FitWidth / FitHeight = force the chosen axis to exactly fill the viewport.
    public enum ZoomMode { Fit, OneToOne, FitWidth, FitHeight, Fill }

    public void SetZoomMode(ZoomMode mode)
    {
        if (_image.Source is not BitmapSource bs) return;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var baselineFit = Math.Min(w / bs.PixelWidth, h / bs.PixelHeight);
        if (baselineFit <= 0) return;

        double s = mode switch
        {
            ZoomMode.Fit       => 1.0,
            ZoomMode.OneToOne  => 1.0 / baselineFit,
            ZoomMode.FitWidth  => (w / bs.PixelWidth) / baselineFit,
            ZoomMode.FitHeight => (h / bs.PixelHeight) / baselineFit,
            ZoomMode.Fill      => Math.Max(w / bs.PixelWidth, h / bs.PixelHeight) / baselineFit,
            _ => 1.0,
        };
        _scale.ScaleX = _scale.ScaleY = s;
        _translate.X = _translate.Y = 0;
    }

    public void OneToOne()
    {
        if (_image.Source is not BitmapSource bs) return;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var fitScale = Math.Min(w / bs.PixelWidth, h / bs.PixelHeight);
        if (fitScale <= 0) return;
        _scale.ScaleX = _scale.ScaleY = 1.0 / fitScale;
        _translate.X = _translate.Y = 0;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // V15-05: Shift + wheel pans horizontally instead of zooming. Matches browser + editor
        // muscle memory. Zoom stays on the plain wheel, vertical pan stays on drag-pan + arrow
        // keys. Delta scale picked so one full wheel notch moves ~80 px at the current zoom,
        // which feels right on both a MX Master and a stock mouse wheel.
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _translate.X += e.Delta > 0 ? 80 : -80;
            e.Handled = true;
            return;
        }

        var factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        var newScale = Math.Clamp(_scale.ScaleX * factor, 0.1, 20);
        var rel = e.GetPosition(_image);
        var cx = _image.ActualWidth / 2;
        var cy = _image.ActualHeight / 2;
        var dx = (rel.X - cx) * (newScale - _scale.ScaleX);
        var dy = (rel.Y - cy) * (newScale - _scale.ScaleY);
        _translate.X -= dx;
        _translate.Y -= dy;
        _scale.ScaleX = _scale.ScaleY = newScale;
        e.Handled = true;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) return;
        _dragStart = e.GetPosition(this);
        _dragOrigin = new Point(_translate.X, _translate.Y);
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null) return;
        var p = e.GetPosition(this);
        _translate.X = _dragOrigin.X + (p.X - _dragStart.Value.X);
        _translate.Y = _dragOrigin.Y + (p.Y - _dragStart.Value.Y);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
    }

    private void OnDouble(object sender, MouseButtonEventArgs e)
    {
        if (Math.Abs(_scale.ScaleX - 1) > 0.001) ResetView(); else OneToOne();
        e.Handled = true;
    }

    public void ZoomBy(double factor)
    {
        var n = Math.Clamp(_scale.ScaleX * factor, 0.1, 20);
        _scale.ScaleX = _scale.ScaleY = n;
    }
}
