using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Controls;

/// <summary>
/// A content host that draws its image with wheel-zoom, drag-pan, and double-click fit/1:1 toggle.
/// Used as the photo canvas.
/// </summary>
public sealed class ZoomPanImage : ContentControl
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
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
        new PropertyMetadata(0.0, (d, e) => ((ZoomPanImage)d)._rotate.Angle = (double)e.NewValue));

    public double Rotation
    {
        get => (double)GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public ZoomPanImage()
    {
        var group = new TransformGroup();
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
        _image.Source = src;
        ResetView();
    }

    public void ResetView()
    {
        _scale.ScaleX = _scale.ScaleY = 1;
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
