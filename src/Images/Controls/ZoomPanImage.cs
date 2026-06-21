using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Controls;

public enum SwipeDirection { Left, Right }

public readonly record struct ZoomPanViewState(double Scale, double TranslateX, double TranslateY);

/// <summary>
/// A content host that draws its image with wheel-zoom, drag-pan, and double-click fit/1:1 toggle.
/// Used as the photo canvas. When the <see cref="Animation"/> DP is set, the view model owns the
/// selected frame index and this control renders that frame without disturbing pan/zoom/rotate state.
/// </summary>
public sealed class ZoomPanImage : ContentControl
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly Grid _visual = new();
    private readonly Viewbox _tileViewbox = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Canvas _tileCanvas = new() { IsHitTestVisible = false };
    private readonly Dictionary<TileKey, Image> _tileImages = new();
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
    private int? _renderedTileLevel;
    private bool _tileRefreshQueued;
    public event EventHandler? ViewChanged;
    public event EventHandler<SwipeDirection>? SwipeNavigate;

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

    public static readonly DependencyProperty TilePyramidProperty = DependencyProperty.Register(
        nameof(TilePyramid), typeof(TilePyramidInfo), typeof(ZoomPanImage),
        new PropertyMetadata(null, (d, e) => ((ZoomPanImage)d).OnTilePyramidChanged((TilePyramidInfo?)e.NewValue)));

    public TilePyramidInfo? TilePyramid
    {
        get => (TilePyramidInfo?)GetValue(TilePyramidProperty);
        set => SetValue(TilePyramidProperty, value);
    }

    public static readonly DependencyProperty AnimationFrameIndexProperty = DependencyProperty.Register(
        nameof(AnimationFrameIndex), typeof(int), typeof(ZoomPanImage),
        new PropertyMetadata(0, (d, e) => ((ZoomPanImage)d).OnAnimationFrameIndexChanged((int)e.NewValue)));

    public int AnimationFrameIndex
    {
        get => (int)GetValue(AnimationFrameIndexProperty);
        set => SetValue(AnimationFrameIndexProperty, value);
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

    public static readonly DependencyProperty InspectorModeProperty = DependencyProperty.Register(
        nameof(InspectorMode), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d).OnInspectorModeChanged((bool)e.NewValue)));

    public bool InspectorMode
    {
        get => (bool)GetValue(InspectorModeProperty);
        set => SetValue(InspectorModeProperty, value);
    }

    public static readonly DependencyProperty UseNearestNeighborScalingProperty = DependencyProperty.Register(
        nameof(UseNearestNeighborScaling), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d).ApplyScalingMode((bool)e.NewValue)));

    public bool UseNearestNeighborScaling
    {
        get => (bool)GetValue(UseNearestNeighborScalingProperty);
        set => SetValue(UseNearestNeighborScalingProperty, value);
    }

    public ZoomPanImage()
    {
        var group = new TransformGroup();
        group.Children.Add(_flip);
        group.Children.Add(_rotate);
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        _visual.RenderTransformOrigin = new Point(0.5, 0.5);
        _visual.RenderTransform = group;
        ApplyScalingMode(UseNearestNeighborScaling);
        _flip.Changed += (_, _) => RaiseViewChanged();
        _rotate.Changed += (_, _) => RaiseViewChanged();
        _scale.Changed += (_, _) => RaiseViewChanged();
        _translate.Changed += (_, _) => RaiseViewChanged();

        _root.ClipToBounds = true;
        _tileViewbox.Child = _tileCanvas;
        _visual.Children.Add(_image);
        _visual.Children.Add(_tileViewbox);
        _root.Children.Add(_visual);
        Content = _root;

        MouseWheel += OnWheel;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseDoubleClick += OnDouble;

        IsManipulationEnabled = true;
        ManipulationStarting += OnManipulationStarting;
        ManipulationDelta += OnManipulationDelta;
        ManipulationInertiaStarting += OnManipulationInertiaStarting;

        Loaded += (_, _) => QueueTileRefresh();
        SizeChanged += (_, _) =>
        {
            ResetView();
            QueueTileRefresh();
            RaiseViewChanged();
        };
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var desired = base.MeasureOverride(constraint);

        // The viewer is a clipping canvas, not document content. Large source pixels should not
        // make the root Grid preserve image width at the expense of the surrounding tool chrome.
        var width = double.IsInfinity(constraint.Width) ? desired.Width : Math.Min(desired.Width, constraint.Width);
        var height = double.IsInfinity(constraint.Height) ? desired.Height : Math.Min(desired.Height, constraint.Height);
        return new Size(Math.Max(0, width), Math.Max(0, height));
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
        QueueTileRefresh();
        RaiseViewChanged();
        Dispatcher.BeginInvoke(new Action(RaiseViewChanged), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnTilePyramidChanged(TilePyramidInfo? pyramid)
    {
        ClearTileImages();
        _renderedTileLevel = null;
        _tileViewbox.Visibility = pyramid is null ? Visibility.Collapsed : Visibility.Visible;
        if (pyramid is not null)
            QueueTileRefresh();
        RaiseViewChanged();
    }

    private void OnAnimationChanged(AnimationSequence? seq)
    {
        ApplyAnimationFrame(seq, AnimationFrameIndex);
    }

    private void OnAnimationFrameIndexChanged(int index)
    {
        ApplyAnimationFrame(Animation, index);
    }

    private void ApplyAnimationFrame(AnimationSequence? seq, int index)
    {
        // Cancel any legacy Source animation and show the explicit frame selected by the
        // workbench. This keeps scrubbing, step commands, copy, export, and the canvas in sync.
        _image.BeginAnimation(Image.SourceProperty, null);
        if (seq is null || seq.Frames.Count < 2)
        {
            _image.Source = Source;
            return;
        }

        _image.Source = seq.Frames[Math.Clamp(index, 0, seq.Frames.Count - 1)];
    }

    public void ResetView()
    {
        _scale.ScaleX = _scale.ScaleY = 1;
        _translate.X = _translate.Y = 0;
        QueueTileRefresh();
    }

    public Matrix GetImageToViewportMatrix()
    {
        if (!TryGetRenderedPixelSize(out var pixelWidth, out var pixelHeight))
            return Matrix.Identity;

        return ImageViewportTransform.Calculate(
            pixelWidth,
            pixelHeight,
            ActualWidth,
            ActualHeight,
            _scale.ScaleX,
            _translate.X,
            _translate.Y,
            _rotate.Angle,
            FlipHorizontal,
            FlipVertical);
    }

    public ZoomPanViewState GetViewState()
        => new(_scale.ScaleX, _translate.X, _translate.Y);

    public void SetViewState(ZoomPanViewState state)
    {
        var scale = double.IsFinite(state.Scale) ? Math.Clamp(state.Scale, 0.1, 20) : 1;
        var translateX = double.IsFinite(state.TranslateX) ? state.TranslateX : 0;
        var translateY = double.IsFinite(state.TranslateY) ? state.TranslateY : 0;

        if (Math.Abs(_scale.ScaleX - scale) < 0.0001 &&
            Math.Abs(_translate.X - translateX) < 0.0001 &&
            Math.Abs(_translate.Y - translateY) < 0.0001)
        {
            return;
        }

        _scale.ScaleX = _scale.ScaleY = scale;
        _translate.X = translateX;
        _translate.Y = translateY;
        QueueTileRefresh();
        RaiseViewChanged();
    }

    // V20-20: four zoom modes. All compute against the source image's pixel size in the
    // control's current available size. Stretch.Uniform on the inner Image handles the baseline
    // fit; our ScaleTransform multiplies on top. Fit = 1.0x (baseline Uniform is already fit);
    // 1:1 = inverse of Uniform's baseline fit so each source pixel maps to one device pixel;
    // FitWidth / FitHeight = force the chosen axis to exactly fill the viewport.
    public enum ZoomMode { Fit, OneToOne, FitWidth, FitHeight, Fill }

    public void SetZoomMode(ZoomMode mode)
    {
        if (!TryGetRenderedPixelSize(out var pixelWidth, out var pixelHeight)) return;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var baselineFit = Math.Min(w / pixelWidth, h / pixelHeight);
        if (baselineFit <= 0) return;

        double s = mode switch
        {
            ZoomMode.Fit       => 1.0,
            ZoomMode.OneToOne  => 1.0 / baselineFit,
            ZoomMode.FitWidth  => (w / pixelWidth) / baselineFit,
            ZoomMode.FitHeight => (h / pixelHeight) / baselineFit,
            ZoomMode.Fill      => Math.Max(w / pixelWidth, h / pixelHeight) / baselineFit,
            _ => 1.0,
        };
        _scale.ScaleX = _scale.ScaleY = s;
        _translate.X = _translate.Y = 0;
        QueueTileRefresh();
        RaiseViewChanged();
    }

    public void OneToOne()
    {
        if (!TryGetRenderedPixelSize(out var pixelWidth, out var pixelHeight)) return;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var fitScale = Math.Min(w / pixelWidth, h / pixelHeight);
        if (fitScale <= 0) return;
        _scale.ScaleX = _scale.ScaleY = 1.0 / fitScale;
        _translate.X = _translate.Y = 0;
        QueueTileRefresh();
        RaiseViewChanged();
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
            QueueTileRefresh();
            RaiseViewChanged();
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
        QueueTileRefresh();
        RaiseViewChanged();
        e.Handled = true;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) return;
        if (InspectorMode)
        {
            Cursor = Cursors.Cross;
            return;
        }

        _dragStart = e.GetPosition(this);
        _dragOrigin = new Point(_translate.X, _translate.Y);
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (InspectorMode)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (_dragStart is null) return;
        var p = e.GetPosition(this);
        _translate.X = _dragOrigin.X + (p.X - _dragStart.Value.X);
        _translate.Y = _dragOrigin.Y + (p.Y - _dragStart.Value.Y);
        QueueTileRefresh();
        RaiseViewChanged();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        ReleaseMouseCapture();
        Cursor = InspectorMode ? Cursors.Cross : Cursors.Arrow;
    }

    private void OnDouble(object sender, MouseButtonEventArgs e)
    {
        if (InspectorMode)
            return;

        if (Math.Abs(_scale.ScaleX - 1) > 0.001) ResetView(); else OneToOne();
        e.Handled = true;
    }

    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        e.Handled = true;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        var pinchScale = Math.Max(e.DeltaManipulation.Scale.X, e.DeltaManipulation.Scale.Y);
        if (Math.Abs(pinchScale - 1.0) > 0.001)
        {
            var newScale = Math.Clamp(_scale.ScaleX * pinchScale, 0.1, 20);
            var center = e.ManipulationOrigin;
            var cx = ActualWidth / 2;
            var cy = ActualHeight / 2;
            var dx = (center.X - cx) * (newScale - _scale.ScaleX);
            var dy = (center.Y - cy) * (newScale - _scale.ScaleY);
            _translate.X -= dx;
            _translate.Y -= dy;
            _scale.ScaleX = _scale.ScaleY = newScale;
        }

        if (IsZoomed)
        {
            _translate.X += e.DeltaManipulation.Translation.X;
            _translate.Y += e.DeltaManipulation.Translation.Y;
        }

        QueueTileRefresh();
        RaiseViewChanged();
        e.Handled = true;
    }

    private void OnManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        var isFit = Math.Abs(_scale.ScaleX - 1.0) < 0.01;
        var vx = e.InitialVelocities.LinearVelocity.X;
        if (isFit && Math.Abs(vx) > 0.5)
        {
            SwipeNavigate?.Invoke(this, vx > 0 ? SwipeDirection.Right : SwipeDirection.Left);
            e.Cancel();
            e.Handled = true;
            return;
        }

        e.TranslationBehavior.DesiredDeceleration = 0.01;
        e.Handled = true;
    }

    public void PanBy(double dx, double dy)
    {
        _translate.X += dx;
        _translate.Y += dy;
        QueueTileRefresh();
        RaiseViewChanged();
    }

    public bool IsZoomed => Math.Abs(_scale.ScaleX - 1.0) > 0.01;

    public void ZoomBy(double factor)
    {
        var n = Math.Clamp(_scale.ScaleX * factor, 0.1, 20);
        _scale.ScaleX = _scale.ScaleY = n;
        QueueTileRefresh();
        RaiseViewChanged();
    }

    private void RaiseViewChanged()
    {
        QueueTileRefresh();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnInspectorModeChanged(bool enabled)
    {
        Cursor = enabled ? Cursors.Cross : Cursors.Arrow;
        if (enabled && IsMouseCaptured)
            ReleaseMouseCapture();
        _dragStart = null;
    }

    private void ApplyScalingMode(bool nearestNeighbor)
    {
        RenderOptions.SetBitmapScalingMode(
            _image,
            nearestNeighbor ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(
            _tileViewbox,
            nearestNeighbor ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
    }

    private bool TryGetRenderedPixelSize(out int pixelWidth, out int pixelHeight)
    {
        if (TilePyramid is { } pyramid)
        {
            pixelWidth = pyramid.SourceWidth;
            pixelHeight = pyramid.SourceHeight;
            return true;
        }

        if (_image.Source is BitmapSource bs)
        {
            pixelWidth = bs.PixelWidth;
            pixelHeight = bs.PixelHeight;
            return true;
        }

        pixelWidth = 0;
        pixelHeight = 0;
        return false;
    }

    private void QueueTileRefresh()
    {
        if (TilePyramid is null || _tileRefreshQueued || !IsLoaded)
            return;

        _tileRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _tileRefreshQueued = false;
            RefreshTileLayer();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RefreshTileLayer()
    {
        if (TilePyramid is not { } pyramid || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var level = TileService.ChooseLevel(pyramid, ActualWidth, ActualHeight, _scale.ScaleX);
        if (_renderedTileLevel != level)
        {
            ClearTileImages();
            _renderedTileLevel = level;
            var (levelWidth, levelHeight) = TileService.GetLevelPixelSize(pyramid, level);
            _tileCanvas.Width = levelWidth;
            _tileCanvas.Height = levelHeight;
        }

        var visibleTiles = TileService.GetVisibleTiles(
            pyramid,
            level,
            ActualWidth,
            ActualHeight,
            _scale.ScaleX,
            _translate.X,
            _translate.Y,
            _rotate.Angle,
            FlipHorizontal,
            FlipVertical);

        var visibleSet = visibleTiles.ToHashSet();
        foreach (var existing in _tileImages.Keys.Where(key => !visibleSet.Contains(key)).ToList())
        {
            _tileCanvas.Children.Remove(_tileImages[existing]);
            _tileImages.Remove(existing);
        }

        foreach (var key in visibleTiles)
        {
            if (_tileImages.ContainsKey(key))
                continue;

            var bitmap = TileService.LoadTileBitmap(pyramid, key);
            if (bitmap is null)
                continue;

            var tile = new Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight,
                Stretch = Stretch.None,
                IsHitTestVisible = false,
            };

            Canvas.SetLeft(tile, key.Column * pyramid.TileSize - (key.Column > 0 ? pyramid.Overlap : 0));
            Canvas.SetTop(tile, key.Row * pyramid.TileSize - (key.Row > 0 ? pyramid.Overlap : 0));
            _tileImages[key] = tile;
            _tileCanvas.Children.Add(tile);
        }
    }

    private void ClearTileImages()
    {
        _tileCanvas.Children.Clear();
        _tileImages.Clear();
    }

    // A-01: surface custom UIA peer so screen readers announce "Image, W by H pixels" on focus
    // instead of the generic ContentControl label.
    protected override AutomationPeer OnCreateAutomationPeer() => new ImageCanvasAutomationPeer(this);
}
