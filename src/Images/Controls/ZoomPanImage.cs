using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
    private readonly SkiaBitmapPresenter _skiaImage = new() { IsHitTestVisible = false };
    private readonly Image _analysisOverlay = new()
    {
        Stretch = Stretch.Uniform,
        IsHitTestVisible = false,
    };
    // RD-04: checkerboard drawn behind the image so transparent pixels read as transparent.
    // Lives inside _visual so it inherits the same flip/rotate/scale transform and stays aligned
    // with (and scales alongside) the image content.
    private readonly Rectangle _transparencyGrid = new()
    {
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private DrawingBrush? _checkerBrush;
    // RD-08: rubber-band zoom-to-selection. The marquee lives in _root (screen space), above the
    // transformed _visual, so it is drawn in raw viewport coordinates.
    private readonly Rectangle _zoomSelectionRect = new()
    {
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
        StrokeThickness = 1,
        Stroke = new SolidColorBrush(Color.FromRgb(0xF5, 0xA9, 0x7F)),
        Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0xA9, 0x7F)),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private Point? _zoomSelectStart;
    // RD-06: hold-the-middle-button loupe. A fixed-size circular lens that magnifies the source
    // pixels under the cursor without changing the base zoom.
    private const double LoupeSize = 180;
    private readonly Ellipse _loupe = new()
    {
        Width = LoupeSize,
        Height = LoupeSize,
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
        StrokeThickness = 2,
        Stroke = new SolidColorBrush(Color.FromRgb(0xF5, 0xA9, 0x7F)),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private readonly ImageBrush _loupeBrush = new() { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
    private bool _loupeActive;
    // Keyboard-invocable loupe (toggled from a command, no cursor to follow): the lens stays pinned
    // to the viewport center and the user pans the image beneath it. A held middle-button pointer
    // loupe temporarily overrides the position; releasing it reverts to center while this stays on.
    private bool _keyboardLoupe;
    private readonly Grid _visual = new();
    private readonly Viewbox _tileViewbox = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Canvas _tileCanvas = new() { IsHitTestVisible = false };
    private readonly Dictionary<TileKey, Image> _tileImages = new();
    private readonly HashSet<TileKey> _loadingTiles = [];
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
    private int _tileLoadGeneration;
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

    public static readonly DependencyProperty AnalysisOverlaySourceProperty = DependencyProperty.Register(
        nameof(AnalysisOverlaySource), typeof(ImageSource), typeof(ZoomPanImage),
        new PropertyMetadata(null, (d, e) => ((ZoomPanImage)d)._analysisOverlay.Source = (ImageSource?)e.NewValue));

    /// <summary>
    /// Transparent pixel-analysis surface rendered in the same transform stack as the image.
    /// </summary>
    public ImageSource? AnalysisOverlaySource
    {
        get => (ImageSource?)GetValue(AnalysisOverlaySourceProperty);
        set => SetValue(AnalysisOverlaySourceProperty, value);
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

    // Non-destructive colour inversion of the displayed still image. The source file is never
    // modified and export is unaffected; the toggle only swaps the shown bitmap for an inverted
    // copy. Animated frames and tile-backed huge images keep their normal rendering.
    public static readonly DependencyProperty InvertColorsProperty = DependencyProperty.Register(
        nameof(InvertColors), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d).RefreshStaticDisplaySource()));

    public bool InvertColors
    {
        get => (bool)GetValue(InvertColorsProperty);
        set => SetValue(InvertColorsProperty, value);
    }

    // RD-05: keep the current zoom factor across image changes (pan re-anchors to center) so a
    // series can be pixel-peeped at a fixed magnification.
    public static readonly DependencyProperty PreserveViewOnSourceChangeProperty = DependencyProperty.Register(
        nameof(PreserveViewOnSourceChange), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false));

    public bool PreserveViewOnSourceChange
    {
        get => (bool)GetValue(PreserveViewOnSourceChangeProperty);
        set => SetValue(PreserveViewOnSourceChangeProperty, value);
    }

    // RD-04: transparency checkerboard.
    public static readonly DependencyProperty ShowTransparencyGridProperty = DependencyProperty.Register(
        nameof(ShowTransparencyGrid), typeof(bool), typeof(ZoomPanImage),
        new PropertyMetadata(false, (d, e) => ((ZoomPanImage)d).UpdateTransparencyGrid()));

    public bool ShowTransparencyGrid
    {
        get => (bool)GetValue(ShowTransparencyGridProperty);
        set => SetValue(ShowTransparencyGridProperty, value);
    }

    public static readonly DependencyProperty TransparencyGridColorAProperty = DependencyProperty.Register(
        nameof(TransparencyGridColorA), typeof(Color), typeof(ZoomPanImage),
        new PropertyMetadata(Color.FromRgb(0x2A, 0x2A, 0x2E), (d, e) => ((ZoomPanImage)d).InvalidateCheckerBrush()));

    public Color TransparencyGridColorA
    {
        get => (Color)GetValue(TransparencyGridColorAProperty);
        set => SetValue(TransparencyGridColorAProperty, value);
    }

    public static readonly DependencyProperty TransparencyGridColorBProperty = DependencyProperty.Register(
        nameof(TransparencyGridColorB), typeof(Color), typeof(ZoomPanImage),
        new PropertyMetadata(Color.FromRgb(0x3A, 0x3A, 0x40), (d, e) => ((ZoomPanImage)d).InvalidateCheckerBrush()));

    public Color TransparencyGridColorB
    {
        get => (Color)GetValue(TransparencyGridColorBProperty);
        set => SetValue(TransparencyGridColorBProperty, value);
    }

    // RD-06: loupe magnification relative to 1:1 (2.0 = 200% of source pixels).
    public static readonly DependencyProperty LoupeFactorProperty = DependencyProperty.Register(
        nameof(LoupeFactor), typeof(double), typeof(ZoomPanImage),
        new PropertyMetadata(2.0));

    public double LoupeFactor
    {
        get => (double)GetValue(LoupeFactorProperty);
        set => SetValue(LoupeFactorProperty, value);
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
        _visual.Children.Add(_transparencyGrid);
        _visual.Children.Add(_skiaImage);
        _visual.Children.Add(_image);
        _visual.Children.Add(_tileViewbox);
        _visual.Children.Add(_analysisOverlay);
        _root.Children.Add(_visual);
        _root.Children.Add(_zoomSelectionRect);
        _loupe.Fill = _loupeBrush;
        _root.Children.Add(_loupe);
        Content = _root;

        _image.SizeChanged += (_, _) => UpdateTransparencyGrid();
        _skiaImage.SizeChanged += (_, _) => UpdateTransparencyGrid();

        MouseWheel += OnWheel;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseDoubleClick += OnDouble;
        MouseDown += OnAnyButtonDown;
        MouseUp += OnAnyButtonUp;
        MouseLeave += (_, _) => StopLoupe();
        LostMouseCapture += (_, _) => OnCaptureLost();

        IsManipulationEnabled = true;
        ManipulationStarting += OnManipulationStarting;
        ManipulationDelta += OnManipulationDelta;
        ManipulationInertiaStarting += OnManipulationInertiaStarting;

        Loaded += (_, _) => QueueTileRefresh();
        SizeChanged += (_, _) => HandleSizeChanged();
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
        SetStaticDisplaySource(src);
        if (PreserveViewOnSourceChange && src is not null)
            RecenterPreservingZoom();
        else
            ResetView();
        UpdateTransparencyGrid();
        QueueTileRefresh();
        RaiseViewChanged();
        Dispatcher.BeginInvoke(new Action(RaiseViewChanged), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private ImageSource? _staticBaseSource;
    private ImageSource? _invertCacheKey;
    private ImageSource? _invertCacheValue;

    // Central point where the still-image display source is set, so the InvertColors toggle can
    // swap in a colour-inverted copy without every caller needing to know about it.
    private void SetStaticDisplaySource(ImageSource? baseSource)
    {
        _staticBaseSource = baseSource;
        SetStaticPresenterSource(InvertColors ? InvertIfPossible(baseSource) : baseSource);
    }

    // Re-applies the current base source through the (possibly toggled) inversion. A running
    // multi-frame animation keeps its frames; only static images invert.
    private void RefreshStaticDisplaySource()
    {
        if (Animation is { Frames.Count: >= 2 })
            return;

        SetStaticPresenterSource(InvertColors ? InvertIfPossible(_staticBaseSource) : _staticBaseSource);
    }

    private void SetStaticPresenterSource(ImageSource? source)
    {
        _image.Source = source;

        try
        {
            var skiaReady = source is BitmapSource bitmap && _skiaImage.SetSource(bitmap);
            SetStillPresenter(skiaReady);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            NotSupportedException or DllNotFoundException or TypeInitializationException or OutOfMemoryException)
        {
            _skiaImage.SetSource(null);
            SetStillPresenter(false);
        }
    }

    private void SetStillPresenter(bool useSkia)
    {
        var canUseSkia = useSkia && TilePyramid is null && Animation is not { Frames.Count: >= 2 };
        _skiaImage.Visibility = canUseSkia ? Visibility.Visible : Visibility.Hidden;
        // Hidden preserves the Image's arranged box for the loupe/checkerboard/overlay contract.
        _image.Visibility = canUseSkia ? Visibility.Hidden : Visibility.Visible;
    }

    internal bool IsSkiaStaticRendererActive =>
        _skiaImage.HasBitmap && _skiaImage.Visibility == Visibility.Visible;

    private ImageSource? InvertIfPossible(ImageSource? source)
    {
        if (source is not BitmapSource bitmap)
            return source;

        if (ReferenceEquals(_invertCacheKey, bitmap) && _invertCacheValue is not null)
            return _invertCacheValue;

        var inverted = Services.ImageInvertService.Invert(bitmap);
        _invertCacheKey = bitmap;
        _invertCacheValue = inverted;
        return inverted;
    }

    // RD-05: keep the current zoom scale but re-anchor the pan to center for the incoming image.
    private void RecenterPreservingZoom()
    {
        _translate.X = _translate.Y = 0;
        QueueTileRefresh();
    }

    private void OnTilePyramidChanged(TilePyramidInfo? pyramid)
    {
        ClearTileImages();
        _renderedTileLevel = null;
        _tileViewbox.Visibility = pyramid is null ? Visibility.Collapsed : Visibility.Visible;
        SetStillPresenter(_skiaImage.HasBitmap);
        if (pyramid is not null)
            QueueTileRefresh();
        UpdateTransparencyGrid();
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
        if (seq is null || seq.Frames.Count < 2)
        {
            _image.BeginAnimation(Image.SourceProperty, null);
            SetStaticDisplaySource(Source);
            return;
        }

        SetStillPresenter(false);
        _image.Source = seq.Frames[Math.Clamp(index, 0, seq.Frames.Count - 1)];
    }

    public void ResetView()
    {
        _scale.ScaleX = _scale.ScaleY = 1;
        _translate.X = _translate.Y = 0;
        QueueTileRefresh();
    }

    private void HandleSizeChanged()
    {
        if (IsUntouchedFitView)
            ResetView();
        else
            QueueTileRefresh();

        UpdateTransparencyGrid();
        RaiseViewChanged();
    }

    private void InvalidateCheckerBrush()
    {
        _checkerBrush = null;
        UpdateTransparencyGrid();
    }

    private DrawingBrush CheckerBrush =>
        _checkerBrush ??= BuildCheckerBrush(TransparencyGridColorA, TransparencyGridColorB, 8);

    // RD-04: size the checkerboard to the image's rendered box and show it only for images that
    // actually carry an alpha channel. Tiled (huge) images and opaque images skip it.
    private void UpdateTransparencyGrid()
    {
        if (!ShowTransparencyGrid || !SourceHasAlpha() ||
            !TryGetRenderedPixelSize(out var pixelWidth, out var pixelHeight) ||
            pixelWidth <= 0 || pixelHeight <= 0)
        {
            _transparencyGrid.Visibility = Visibility.Collapsed;
            return;
        }

        var availableWidth = _image.ActualWidth > 0 ? _image.ActualWidth : ActualWidth;
        var availableHeight = _image.ActualHeight > 0 ? _image.ActualHeight : ActualHeight;
        var fit = Math.Min(availableWidth / pixelWidth, availableHeight / pixelHeight);
        if (!double.IsFinite(fit) || fit <= 0)
        {
            _transparencyGrid.Visibility = Visibility.Collapsed;
            return;
        }

        _transparencyGrid.Width = pixelWidth * fit;
        _transparencyGrid.Height = pixelHeight * fit;
        _transparencyGrid.Fill = CheckerBrush;
        _transparencyGrid.Visibility = Visibility.Visible;
    }

    private bool SourceHasAlpha()
        => TilePyramid is null && _image.Source is BitmapSource bs && HasAlphaChannel(bs.Format);

    internal static bool HasAlphaChannel(PixelFormat format)
        => format == PixelFormats.Bgra32
        || format == PixelFormats.Pbgra32
        || format == PixelFormats.Rgba64
        || format == PixelFormats.Prgba64
        || format == PixelFormats.Rgba128Float
        || format == PixelFormats.Prgba128Float;

    private static DrawingBrush BuildCheckerBrush(Color colorA, Color colorB, double cell)
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(colorA), null, new RectangleGeometry(new Rect(0, 0, cell * 2, cell * 2))));
        var contrast = new SolidColorBrush(colorB);
        group.Children.Add(new GeometryDrawing(contrast, null, new RectangleGeometry(new Rect(0, 0, cell, cell))));
        group.Children.Add(new GeometryDrawing(contrast, null, new RectangleGeometry(new Rect(cell, cell, cell, cell))));

        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, cell * 2, cell * 2),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    private bool IsUntouchedFitView =>
        Math.Abs(_scale.ScaleX - 1.0) < 0.0001 &&
        Math.Abs(_translate.X) < 0.0001 &&
        Math.Abs(_translate.Y) < 0.0001;

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
        ZoomAroundViewportPoint(e.GetPosition(this), newScale);
        QueueTileRefresh();
        RaiseViewChanged();
        e.Handled = true;
    }

    /// <summary>
    /// Rescales while keeping the content under <paramref name="anchor"/>
    /// (viewport coordinates) fixed. The anchor offset is measured in the
    /// post-transform frame, so rotation, flips, and the current pan are all
    /// accounted for — compensating with raw image-local offsets made zoom
    /// lurch away from the cursor on rotated or flipped images.
    /// </summary>
    private void ZoomAroundViewportPoint(Point anchor, double newScale)
    {
        var currentScale = _scale.ScaleX;
        if (currentScale > 0.0001)
        {
            var offsetX = anchor.X - ActualWidth / 2 - _translate.X;
            var offsetY = anchor.Y - ActualHeight / 2 - _translate.Y;
            _translate.X -= offsetX * (newScale - currentScale) / currentScale;
            _translate.Y -= offsetY * (newScale - currentScale) / currentScale;
        }

        _scale.ScaleX = _scale.ScaleY = newScale;
    }

    private const ModifierKeys ZoomSelectModifiers = ModifierKeys.Control | ModifierKeys.Shift;

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2) return;
        // While the loupe is held (middle button), left-button gestures are suppressed so a
        // pan/zoom-select cannot start underneath it.
        if (_loupeActive) return;

        // RD-08: Ctrl+Shift+drag draws a marquee that zooms to the boxed region. Checked before
        // pan/inspector so it wins the gesture; needs a rendered image to have something to zoom.
        if ((Keyboard.Modifiers & ZoomSelectModifiers) == ZoomSelectModifiers &&
            TryGetRenderedPixelSize(out _, out _))
        {
            _zoomSelectStart = e.GetPosition(this);
            UpdateZoomSelectionRect(_zoomSelectStart.Value, _zoomSelectStart.Value);
            _zoomSelectionRect.Visibility = Visibility.Visible;
            CaptureMouse();
            Cursor = Cursors.Cross;
            e.Handled = true;
            return;
        }

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

    // RD-06: middle-button hold shows the loupe; release hides it.
    private void OnAnyButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Loupe is mutually exclusive with an in-progress pan or zoom-select gesture.
        if (e.ChangedButton == MouseButton.Middle && _dragStart is null && _zoomSelectStart is null)
            StartLoupe(e.GetPosition(this));
    }

    private void OnAnyButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
            StopLoupe();
    }

    private void StartLoupe(Point position)
    {
        // Needs a real raster source with usable pixels. Tile-backed (gigapixel) images expose
        // only a 1x1 placeholder as _image.Source, which would magnify into a solid block.
        if (_image.Source is not BitmapSource { PixelWidth: > 1 } || !TryGetRenderedPixelSize(out _, out _))
            return;
        _loupeActive = true;
        _loupe.Visibility = Visibility.Visible;
        // Capture so a middle-release outside the control still hides the loupe.
        CaptureMouse();
        UpdateLoupe(position);
    }

    private void StopLoupe()
    {
        if (!_loupeActive) return;
        _loupeActive = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        // A keyboard loupe outlives the pointer gesture: revert the lens to viewport center
        // instead of hiding it.
        if (_keyboardLoupe)
            UpdateLoupe(ViewportCenter);
        else
            _loupe.Visibility = Visibility.Collapsed;
    }

    public bool IsLoupeActive => _loupeActive;

    public bool IsKeyboardLoupeActive => _keyboardLoupe;

    private Point ViewportCenter => new(ActualWidth / 2, ActualHeight / 2);

    /// <summary>
    /// Toggles a viewport-centered magnifier that needs no pointer. Returns the resulting active
    /// state. Requires a real raster source (tile-backed placeholders can't be magnified).
    /// </summary>
    public bool ToggleKeyboardLoupe()
    {
        if (_keyboardLoupe)
        {
            _keyboardLoupe = false;
            if (!_loupeActive)
                _loupe.Visibility = Visibility.Collapsed;
            return false;
        }

        if (_image.Source is not BitmapSource { PixelWidth: > 1 } || !TryGetRenderedPixelSize(out _, out _))
            return false;

        _keyboardLoupe = true;
        _loupe.Visibility = Visibility.Visible;
        UpdateLoupe(ViewportCenter);
        return true;
    }

    // Keep the centered lens sampling whatever the current pan/zoom puts under the viewport center.
    // Drops itself if the incoming source is no longer a magnifiable raster (e.g. a tiled image).
    private void RefreshKeyboardLoupe()
    {
        if (!_keyboardLoupe || _loupeActive) return;
        if (_image.Source is not BitmapSource { PixelWidth: > 1 })
        {
            _keyboardLoupe = false;
            _loupe.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateLoupe(ViewportCenter);
    }

    private void UpdateLoupe(Point cursor)
    {
        if (!TryGetRenderedPixelSize(out var pixelWidth, out var pixelHeight))
        {
            StopLoupe();
            if (_keyboardLoupe)
            {
                _keyboardLoupe = false;
                _loupe.Visibility = Visibility.Collapsed;
            }
            return;
        }

        var matrix = GetImageToViewportMatrix();
        if (!matrix.HasInverse)
            return;
        matrix.Invert();
        var src = matrix.Transform(cursor);

        // At the loupe factor, LoupeSize screen pixels show (LoupeSize / factor) source pixels.
        var region = LoupeSize / Math.Max(0.1, LoupeFactor);
        _loupeBrush.ImageSource = _image.Source;
        _loupeBrush.Viewbox = ComputeLoupeViewbox(src.X, src.Y, pixelWidth, pixelHeight, region, region);
        _loupe.Margin = new Thickness(cursor.X - LoupeSize / 2, cursor.Y - LoupeSize / 2, 0, 0);
    }

    /// <summary>Normalized, clamped source rectangle the loupe samples around (<paramref name="srcX"/>, <paramref name="srcY"/>).</summary>
    internal static Rect ComputeLoupeViewbox(double srcX, double srcY, int pixelWidth, int pixelHeight, double regionWidth, double regionHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return new Rect(0, 0, 1, 1);

        var w = Math.Min(1.0, regionWidth / pixelWidth);
        var h = Math.Min(1.0, regionHeight / pixelHeight);
        var x = Math.Clamp((srcX - regionWidth / 2) / pixelWidth, 0, Math.Max(0, 1 - w));
        var y = Math.Clamp((srcY - regionHeight / 2) / pixelHeight, 0, Math.Max(0, 1 - h));
        return new Rect(x, y, w, h);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_loupeActive)
        {
            UpdateLoupe(e.GetPosition(this));
            return;
        }

        if (_zoomSelectStart is { } start)
        {
            UpdateZoomSelectionRect(start, e.GetPosition(this));
            return;
        }

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
        if (_zoomSelectStart is { } start)
        {
            var rect = MakeRect(start, e.GetPosition(this));
            CancelZoomSelection();
            ApplyZoomToViewportRect(rect);
            return;
        }

        _dragStart = null;
        ReleaseMouseCapture();
        Cursor = InspectorMode ? Cursors.Cross : Cursors.Arrow;
    }

    /// <summary>Abort an in-progress rubber-band zoom selection (e.g. on Escape or capture loss).</summary>
    public void CancelZoomSelection()
    {
        if (_zoomSelectStart is null) return;
        _zoomSelectStart = null;
        _zoomSelectionRect.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Cursor = InspectorMode ? Cursors.Cross : Cursors.Arrow;
    }

    // Capture was stolen (system dialog, alt-tab, etc.): clear any active loupe/zoom-select
    // state. Capture is already gone here, so do not call ReleaseMouseCapture (avoids re-entry).
    private void OnCaptureLost()
    {
        if (_zoomSelectStart is not null)
        {
            _zoomSelectStart = null;
            _zoomSelectionRect.Visibility = Visibility.Collapsed;
            Cursor = InspectorMode ? Cursors.Cross : Cursors.Arrow;
        }
        if (_loupeActive)
        {
            _loupeActive = false;
            if (_keyboardLoupe)
                UpdateLoupe(ViewportCenter);
            else
                _loupe.Visibility = Visibility.Collapsed;
        }
    }

    public bool IsZoomSelecting => _zoomSelectStart is not null;

    private void UpdateZoomSelectionRect(Point a, Point b)
    {
        var rect = MakeRect(a, b);
        _zoomSelectionRect.Margin = new Thickness(rect.X, rect.Y, 0, 0);
        _zoomSelectionRect.Width = rect.Width;
        _zoomSelectionRect.Height = rect.Height;
    }

    private static Rect MakeRect(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // RD-08: rescale so the boxed viewport region fills the viewport, then recenter it.
    internal void ApplyZoomToViewportRect(Rect selection)
    {
        // Ignore accidental click-sized boxes — treat those as a no-op, not a huge zoom.
        if (selection.Width < 6 || selection.Height < 6) return;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var factor = Math.Min(w / selection.Width, h / selection.Height);
        var newScale = Math.Clamp(_scale.ScaleX * factor, 0.1, 20);
        var center = new Point(selection.X + selection.Width / 2, selection.Y + selection.Height / 2);

        ZoomAroundViewportPoint(center, newScale);
        _translate.X += w / 2 - center.X;
        _translate.Y += h / 2 - center.Y;

        QueueTileRefresh();
        RaiseViewChanged();
    }

    private void OnDouble(object sender, MouseButtonEventArgs e)
    {
        if (InspectorMode || e.ChangedButton != MouseButton.Left)
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
            ZoomAroundViewportPoint(e.ManipulationOrigin, newScale);
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
        RefreshKeyboardLoupe();
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
        _skiaImage.SetNearestNeighbor(nearestNeighbor);
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
        _loadingTiles.RemoveWhere(key => !visibleSet.Contains(key));

        foreach (var key in visibleTiles)
        {
            if (_tileImages.ContainsKey(key) || _loadingTiles.Contains(key))
                continue;

            _loadingTiles.Add(key);
            _ = LoadTileBitmapAsync(pyramid, key, _tileLoadGeneration);
        }
    }

    private async Task LoadTileBitmapAsync(TilePyramidInfo pyramid, TileKey key, int generation)
    {
        var bitmap = await Task.Run(() => TileService.LoadTileBitmap(pyramid, key));
        if (generation != _tileLoadGeneration ||
            TilePyramid != pyramid ||
            _renderedTileLevel != key.Level ||
            !_loadingTiles.Remove(key) ||
            bitmap is null ||
            _tileImages.ContainsKey(key))
        {
            return;
        }

        AddTileImage(pyramid, key, bitmap);
    }

    private void AddTileImage(TilePyramidInfo pyramid, TileKey key, BitmapSource bitmap)
    {
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

    private void ClearTileImages()
    {
        _tileLoadGeneration++;
        _tileCanvas.Children.Clear();
        _tileImages.Clear();
        _loadingTiles.Clear();
    }

    // A-01: surface custom UIA peer so screen readers announce "Image, W by H pixels" on focus
    // instead of the generic ContentControl label.
    protected override AutomationPeer OnCreateAutomationPeer() => new ImageCanvasAutomationPeer(this);
}
