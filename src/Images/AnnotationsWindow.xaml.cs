using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Images.Localization;
using Images.Services;

namespace Images;

public partial class AnnotationsWindow : Window
{
    private readonly string _imagePath;
    private readonly Action<ImageAnnotationPlan> _apply;
    private readonly List<ImageAnnotationItem> _items = [];
    private readonly List<ImageAnnotationPoint> _freehandPoints = [];
    private ImageAnnotationKind _tool = ImageAnnotationKind.Arrow;
    private string _color = "#F38BA8";
    private int _nextNumber = 1;
    private int _imageWidth;
    private int _imageHeight;
    private Point? _dragStart;
    private bool _isDragging;
    private bool _hasLoadError;

    public AnnotationsWindow(string imagePath, Action<ImageAnnotationPlan> apply)
    {
        _imagePath = imagePath;
        _apply = apply;

        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };

        LoadPreview();
        UpdateStyleText();
        UpdateToolStatus();
        UpdateStatus();
    }

    private void LoadPreview()
    {
        if (!File.Exists(_imagePath))
        {
            ShowLoadErrorStatus();
            return;
        }

        try
        {
            var bitmap = ImageLoader.LoadPreviewImage(_imagePath, int.MaxValue) as BitmapSource
                ?? throw new InvalidOperationException("The annotation preview did not produce a bitmap.");

            _imageWidth = bitmap.PixelWidth;
            _imageHeight = bitmap.PixelHeight;
            _hasLoadError = false;
            PreviewImage.Source = bitmap;
            ResizePreviewImage();
        }
        catch (Exception ex) when (
            ex is NotSupportedException or
                  FileFormatException or
                  InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException or
                  ArgumentException or
                  ImageMagick.MagickException)
        {
            ShowLoadErrorStatus();
        }
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } &&
            Enum.TryParse<ImageAnnotationKind>(tag, ignoreCase: true, out var tool))
        {
            _tool = tool;
            UpdateToolStatus();
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            _color = tag;
            UpdateToolStatus();
        }
    }

    private void StyleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateStyleText();

    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizePreviewImage();
        Redraw();
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_imageWidth <= 0 || _imageHeight <= 0 || !TryGetImagePoint(e.GetPosition(PreviewCanvas), out var imagePoint))
            return;

        if (_tool is ImageAnnotationKind.Text)
        {
            AddItem(CreateTextItem(imagePoint));
            return;
        }

        if (_tool is ImageAnnotationKind.Number)
        {
            AddItem(CreateNumberItem(imagePoint));
            return;
        }

        _dragStart = imagePoint;
        _isDragging = true;
        _freehandPoints.Clear();
        if (_tool is ImageAnnotationKind.Freehand)
            _freehandPoints.Add(new ImageAnnotationPoint(imagePoint.X, imagePoint.Y));

        PreviewCanvas.CaptureMouse();
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!TryGetImagePoint(e.GetPosition(PreviewCanvas), out var current))
            return;

        if (_tool is ImageAnnotationKind.Freehand)
        {
            if (_freehandPoints.Count == 0 || Distance(_freehandPoints[^1].X, _freehandPoints[^1].Y, current.X, current.Y) >= 2)
                _freehandPoints.Add(new ImageAnnotationPoint(current.X, current.Y));

            Redraw(CreateFreehandItem());
            return;
        }

        Redraw(CreateDragItem(start, current));
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _dragStart is not { } start)
            return;

        _isDragging = false;
        _dragStart = null;
        if (PreviewCanvas.IsMouseCaptured)
            PreviewCanvas.ReleaseMouseCapture();

        if (!TryGetImagePoint(e.GetPosition(PreviewCanvas), out var current))
        {
            Redraw();
            return;
        }

        var item = _tool is ImageAnnotationKind.Freehand
            ? CreateFreehandItem()
            : CreateDragItem(start, current);
        AddItem(item);
    }

    private void PreviewCanvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
        _dragStart = null;
        _freehandPoints.Clear();
        Redraw();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplyAndClose();

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
            return;

        _items.RemoveAt(_items.Count - 1);
        Redraw();
        UpdateStatus();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        _nextNumber = 1;
        Redraw();
        UpdateStatus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!ShouldHandleWindowKey(e.Key, Keyboard.FocusedElement))
            return;

        if (e.Key == Key.Enter)
        {
            ApplyAndClose();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    internal static bool ShouldHandleWindowKey(Key key, object? focusedElement)
        => key is Key.Enter or Key.Escape && !MainWindow.IsTextEntryElement(focusedElement);

    private void ApplyAndClose()
    {
        if (_hasLoadError)
            return;

        var plan = ImageAnnotationService.Normalize(new ImageAnnotationPlan(_items.ToList()));
        if (plan.IsEmpty)
        {
            StatusText.Text = Strings.AnnotationApplyMissingItem;
            return;
        }

        _apply(plan);
        Close();
    }

    private void AddItem(ImageAnnotationItem item)
    {
        var plan = ImageAnnotationService.Normalize(new ImageAnnotationPlan([item]));
        if (plan.IsEmpty)
        {
            Redraw();
            return;
        }

        _items.Add(plan.Items[0]);
        if (item.Kind is ImageAnnotationKind.Number)
            _nextNumber++;

        Redraw();
        UpdateStatus();
    }

    private ImageAnnotationItem CreateDragItem(Point start, Point current)
    {
        var width = current.X - start.X;
        var height = current.Y - start.Y;
        return new ImageAnnotationItem(
            _tool,
            start.X,
            start.Y,
            width,
            height,
            current.X,
            current.Y,
            AnnotationTextBox.Text,
            _nextNumber,
            _color,
            StrokeSlider.Value,
            FontSlider.Value,
            _tool is ImageAnnotationKind.Arrow ? CreateBezierArrowPoints(start, current) : []);
    }

    private ImageAnnotationItem CreateTextItem(Point point)
        => new(
            ImageAnnotationKind.Text,
            point.X,
            point.Y,
            0,
            0,
            point.X,
            point.Y,
            string.IsNullOrWhiteSpace(AnnotationTextBox.Text) ? Strings.AnnotationDefaultText : AnnotationTextBox.Text,
            _nextNumber,
            _color,
            StrokeSlider.Value,
            FontSlider.Value,
            []);

    private ImageAnnotationItem CreateNumberItem(Point point)
        => new(
            ImageAnnotationKind.Number,
            point.X,
            point.Y,
            0,
            0,
            point.X,
            point.Y,
            "",
            _nextNumber,
            _color,
            StrokeSlider.Value,
            FontSlider.Value,
            []);

    private ImageAnnotationItem CreateFreehandItem()
        => new(
            ImageAnnotationKind.Freehand,
            0,
            0,
            0,
            0,
            0,
            0,
            "",
            _nextNumber,
            _color,
            StrokeSlider.Value,
            FontSlider.Value,
            _freehandPoints.ToList());

    private void Redraw(ImageAnnotationItem? preview = null)
    {
        while (PreviewCanvas.Children.Count > 1)
            PreviewCanvas.Children.RemoveAt(1);

        foreach (var item in _items)
            DrawOverlayItem(item, isPreview: false);

        if (preview is not null)
            DrawOverlayItem(preview, isPreview: true);
    }

    private void DrawOverlayItem(ImageAnnotationItem item, bool isPreview)
    {
        var brush = BrushFor(item.Color, isPreview ? 0.72 : 0.95);
        var thickness = Math.Max(1, item.StrokeWidth * CurrentScale());

        switch (item.Kind)
        {
            case ImageAnnotationKind.Rectangle:
            case ImageAnnotationKind.Blur:
            case ImageAnnotationKind.Pixelate:
                DrawOverlayRectangle(item, brush, thickness);
                break;
            case ImageAnnotationKind.Ellipse:
                DrawOverlayEllipse(item, brush, thickness);
                break;
            case ImageAnnotationKind.Arrow:
                DrawOverlayArrow(item, brush, thickness);
                break;
            case ImageAnnotationKind.Text:
                DrawOverlayText(item, brush);
                break;
            case ImageAnnotationKind.Number:
                DrawOverlayNumber(item, brush);
                break;
            case ImageAnnotationKind.Freehand:
                DrawOverlayFreehand(item, brush, thickness);
                break;
        }
    }

    private void DrawOverlayRectangle(ImageAnnotationItem item, Brush brush, double thickness)
    {
        var rect = ToCanvasRect(item);
        var shape = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = item.Kind is ImageAnnotationKind.Blur or ImageAnnotationKind.Pixelate
                ? BrushFor(item.Color, 0.18)
                : Brushes.Transparent
        };
        Canvas.SetLeft(shape, rect.X);
        Canvas.SetTop(shape, rect.Y);
        PreviewCanvas.Children.Add(shape);
    }

    private void DrawOverlayEllipse(ImageAnnotationItem item, Brush brush, double thickness)
    {
        var rect = ToCanvasRect(item);
        var shape = new Ellipse
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(shape, rect.X);
        Canvas.SetTop(shape, rect.Y);
        PreviewCanvas.Children.Add(shape);
    }

    private void DrawOverlayArrow(ImageAnnotationItem item, Brush brush, double thickness)
    {
        var start = ToCanvasPoint(new Point(item.X, item.Y));
        var end = ToCanvasPoint(new Point(item.EndX, item.EndY));
        var tangentStart = start;

        if (item.Points.Count >= 4)
        {
            var c1 = ToCanvasPoint(new Point(item.Points[1].X, item.Points[1].Y));
            var c2 = ToCanvasPoint(new Point(item.Points[2].X, item.Points[2].Y));
            var path = new System.Windows.Shapes.Path
            {
                Stroke = brush,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = new PathGeometry(
                [
                    new PathFigure(start, [new BezierSegment(c1, c2, end, isStroked: true)], closed: false)
                ])
            };
            PreviewCanvas.Children.Add(path);
            tangentStart = c2;
        }
        else
        {
            AddLine(start, end, brush, thickness);
        }

        var angle = Math.Atan2(end.Y - tangentStart.Y, end.X - tangentStart.X);
        var head = Math.Max(10, thickness * 4);
        AddLine(end, new Point(end.X + Math.Cos(angle + Math.PI * 0.82) * head, end.Y + Math.Sin(angle + Math.PI * 0.82) * head), brush, thickness);
        AddLine(end, new Point(end.X + Math.Cos(angle - Math.PI * 0.82) * head, end.Y + Math.Sin(angle - Math.PI * 0.82) * head), brush, thickness);
    }

    private void DrawOverlayText(ImageAnnotationItem item, Brush brush)
    {
        var point = ToCanvasPoint(new Point(item.X, item.Y));
        var text = new TextBlock
        {
            Text = item.Text,
            Foreground = brush,
            FontWeight = FontWeights.SemiBold,
            FontSize = item.FontSize * CurrentScale(),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(80, PreviewCanvas.ActualWidth - point.X - 8)
        };
        Canvas.SetLeft(text, point.X);
        Canvas.SetTop(text, point.Y);
        PreviewCanvas.Children.Add(text);
    }

    private void DrawOverlayNumber(ImageAnnotationItem item, Brush brush)
    {
        var point = ToCanvasPoint(new Point(item.X, item.Y));
        var radius = Math.Max(12, item.FontSize * CurrentScale() * 0.55);
        var circle = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = brush,
            Stroke = brush,
            StrokeThickness = 1
        };
        Canvas.SetLeft(circle, point.X - radius);
        Canvas.SetTop(circle, point.Y - radius);
        PreviewCanvas.Children.Add(circle);

        var label = new TextBlock
        {
            Text = item.Number.ToString(CultureInfo.InvariantCulture),
            Foreground = ContrastingTextBrushFor(item.Color),
            FontWeight = FontWeights.Bold,
            FontSize = radius,
            Width = radius * 2,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(label, point.X - radius);
        Canvas.SetTop(label, point.Y - radius * 0.74);
        PreviewCanvas.Children.Add(label);
    }

    private void DrawOverlayFreehand(ImageAnnotationItem item, Brush brush, double thickness)
    {
        if (item.Points.Count < 2)
            return;

        var line = new Polyline
        {
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        foreach (var point in item.Points)
            line.Points.Add(ToCanvasPoint(new Point(point.X, point.Y)));

        PreviewCanvas.Children.Add(line);
    }

    private void AddLine(Point start, Point end, Brush brush, double thickness)
    {
        PreviewCanvas.Children.Add(new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
    }

    private static IReadOnlyList<ImageAnnotationPoint> CreateBezierArrowPoints(Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var offset = Math.Min(80, Math.Max(18, length * 0.18));
        var nx = -dy / length * offset;
        var ny = dx / length * offset;
        return
        [
            new ImageAnnotationPoint(start.X, start.Y),
            new ImageAnnotationPoint(start.X + dx * 0.34 + nx, start.Y + dy * 0.34 + ny),
            new ImageAnnotationPoint(start.X + dx * 0.66 + nx, start.Y + dy * 0.66 + ny),
            new ImageAnnotationPoint(end.X, end.Y)
        ];
    }

    private bool TryGetImagePoint(Point canvasPoint, out Point imagePoint)
    {
        imagePoint = default;
        var rect = ImageDisplayRect();
        if (rect.Width <= 0 ||
            rect.Height <= 0 ||
            canvasPoint.X < rect.X ||
            canvasPoint.Y < rect.Y ||
            canvasPoint.X > rect.X + rect.Width ||
            canvasPoint.Y > rect.Y + rect.Height)
        {
            return false;
        }

        var scale = rect.Width / _imageWidth;
        imagePoint = new Point(
            Math.Clamp((canvasPoint.X - rect.X) / scale, 0, Math.Max(0, _imageWidth - 1)),
            Math.Clamp((canvasPoint.Y - rect.Y) / scale, 0, Math.Max(0, _imageHeight - 1)));
        return true;
    }

    private Point ToCanvasPoint(Point imagePoint)
    {
        var rect = ImageDisplayRect();
        var scale = CurrentScale();
        return new Point(rect.X + imagePoint.X * scale, rect.Y + imagePoint.Y * scale);
    }

    private Rect ToCanvasRect(ImageAnnotationItem item)
    {
        var start = ToCanvasPoint(new Point(Math.Min(item.X, item.X + item.Width), Math.Min(item.Y, item.Y + item.Height)));
        var end = ToCanvasPoint(new Point(Math.Max(item.X, item.X + item.Width), Math.Max(item.Y, item.Y + item.Height)));
        return new Rect(start, end);
    }

    private Rect ImageDisplayRect()
    {
        if (_imageWidth <= 0 || _imageHeight <= 0 || PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0)
            return Rect.Empty;

        var scale = Math.Min(PreviewCanvas.ActualWidth / _imageWidth, PreviewCanvas.ActualHeight / _imageHeight);
        var width = _imageWidth * scale;
        var height = _imageHeight * scale;
        return new Rect((PreviewCanvas.ActualWidth - width) / 2, (PreviewCanvas.ActualHeight - height) / 2, width, height);
    }

    private double CurrentScale()
        => _imageWidth <= 0 ? 1 : ImageDisplayRect().Width / _imageWidth;

    private void ResizePreviewImage()
    {
        PreviewImage.Width = PreviewCanvas.ActualWidth;
        PreviewImage.Height = PreviewCanvas.ActualHeight;
    }

    private void UpdateStyleText()
    {
        if (StrokeText is null || FontText is null)
            return;

        StrokeText.Text = Strings.Format("ValuePixelsFormat", StrokeSlider.Value.ToString("0", CultureInfo.InvariantCulture));
        FontText.Text = Strings.Format("ValuePixelsFormat", FontSlider.Value.ToString("0", CultureInfo.InvariantCulture));
    }

    private void UpdateToolStatus()
    {
        ToolStatusText.Text = _tool switch
        {
            ImageAnnotationKind.Text => Strings.AnnotationTextToolStatus,
            ImageAnnotationKind.Number => Strings.AnnotationStepToolStatus,
            ImageAnnotationKind.Freehand => Strings.AnnotationPenToolStatus,
            ImageAnnotationKind.Blur => Strings.AnnotationBlurToolStatus,
            ImageAnnotationKind.Pixelate => Strings.AnnotationPixelateToolStatus,
            ImageAnnotationKind.Arrow => Strings.AnnotationArrowToolStatus,
            ImageAnnotationKind.Ellipse => Strings.AnnotationCircleToolStatus,
            _ => Strings.AnnotationBoxToolStatus
        };
    }

    private void UpdateStatus()
    {
        if (_hasLoadError)
        {
            ApplyButton.IsEnabled = false;
            return;
        }

        ApplyButton.IsEnabled = _items.Count > 0;
        StatusText.Text = _items.Count == 0
            ? Strings.AnnotationNoItemsStatus
            : Strings.Format("AnnotationReadyStatusFormat", _items.Count, _items.Count == 1 ? "" : "s");
    }

    private void ShowLoadErrorStatus()
    {
        _hasLoadError = true;
        StatusText.Text = Strings.AnnotationUnavailableMissingFile;
        ApplyButton.IsEnabled = false;
    }

    private static Brush BrushFor(string color, double opacity)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
            brush.Opacity = opacity;
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Red;
        }
    }

    internal static Brush ContrastingTextBrushFor(string color)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
            var value = (0.2126 * Linearize(brush.Color.R)) +
                        (0.7152 * Linearize(brush.Color.G)) +
                        (0.0722 * Linearize(brush.Color.B));
            return value > 0.42 ? Brushes.Black : Brushes.White;
        }
        catch
        {
            return Brushes.White;
        }
    }

    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
}
