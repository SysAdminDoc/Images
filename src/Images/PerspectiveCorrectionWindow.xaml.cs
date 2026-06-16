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

public partial class PerspectiveCorrectionWindow : Window
{
    private readonly string _imagePath;
    private readonly Action<PerspectiveCorrectionPlan> _apply;
    private readonly List<PerspectivePoint> _points = [];
    private int _imageWidth;
    private int _imageHeight;
    private int? _dragHandleIndex;

    public PerspectiveCorrectionWindow(string imagePath, Action<PerspectiveCorrectionPlan> apply)
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
        ResetCorners();
    }

    private void LoadPreview()
    {
        if (!File.Exists(_imagePath))
        {
            StatusText.Text = Strings.PerspectiveUnavailableMissingFile;
            ApplyButton.IsEnabled = false;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(_imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        _imageWidth = bitmap.PixelWidth;
        _imageHeight = bitmap.PixelHeight;
        PreviewImage.Source = bitmap;
        ResizePreviewImage();
    }

    private void ResetCorners()
    {
        _points.Clear();
        var identity = PerspectiveCorrectionService.Identity(_imageWidth, _imageHeight);
        _points.Add(identity.TopLeft);
        _points.Add(identity.TopRight);
        _points.Add(identity.BottomRight);
        _points.Add(identity.BottomLeft);
        Redraw();
    }

    private void InsetCorners()
    {
        if (_imageWidth <= 0 || _imageHeight <= 0)
            return;

        var xInset = Math.Max(1, _imageWidth * 0.06);
        var yInset = Math.Max(1, _imageHeight * 0.06);
        _points.Clear();
        _points.Add(new PerspectivePoint(xInset, yInset));
        _points.Add(new PerspectivePoint(_imageWidth - 1 - xInset, yInset));
        _points.Add(new PerspectivePoint(_imageWidth - 1 - xInset, _imageHeight - 1 - yInset));
        _points.Add(new PerspectivePoint(xInset, _imageHeight - 1 - yInset));
        Redraw();
    }

    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizePreviewImage();
        Redraw();
    }

    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(PreviewCanvas);
        var handle = HitTestHandle(position);
        if (handle is null)
            return;

        _dragHandleIndex = handle.Value;
        PreviewCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragHandleIndex is not { } index || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!TryGetImagePoint(e.GetPosition(PreviewCanvas), out var point))
            return;

        _points[index] = point;
        Redraw();
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void PreviewCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndDrag();
    }

    private void EndDrag()
    {
        _dragHandleIndex = null;
        if (PreviewCanvas.IsMouseCaptured)
            PreviewCanvas.ReleaseMouseCapture();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => ApplyAndClose();

    private void ResetButton_Click(object sender, RoutedEventArgs e) => ResetCorners();

    private void InsetButton_Click(object sender, RoutedEventArgs e) => InsetCorners();

    private void TopNarrowButton_Click(object sender, RoutedEventArgs e) => ApplyKeystone(topInset: true);

    private void BottomNarrowButton_Click(object sender, RoutedEventArgs e) => ApplyKeystone(bottomInset: true);

    private void LeftShortButton_Click(object sender, RoutedEventArgs e) => ApplyKeystone(leftInset: true);

    private void RightShortButton_Click(object sender, RoutedEventArgs e) => ApplyKeystone(rightInset: true);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
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

    private void ApplyAndClose()
    {
        var plan = CurrentPlan();
        if (PerspectiveCorrectionService.IsIdentity(plan, _imageWidth, _imageHeight))
        {
            StatusText.Text = Strings.PerspectiveMoveCornerBeforeApplying;
            return;
        }

        _apply(plan);
        Close();
    }

    private void ApplyKeystone(
        bool topInset = false,
        bool bottomInset = false,
        bool leftInset = false,
        bool rightInset = false)
    {
        if (_points.Count != 4 || _imageWidth <= 0 || _imageHeight <= 0)
            return;

        var xStep = Math.Max(1, _imageWidth * 0.03);
        var yStep = Math.Max(1, _imageHeight * 0.03);

        if (topInset)
        {
            _points[0] = new PerspectivePoint(_points[0].X + xStep, _points[0].Y);
            _points[1] = new PerspectivePoint(_points[1].X - xStep, _points[1].Y);
        }

        if (bottomInset)
        {
            _points[3] = new PerspectivePoint(_points[3].X + xStep, _points[3].Y);
            _points[2] = new PerspectivePoint(_points[2].X - xStep, _points[2].Y);
        }

        if (leftInset)
        {
            _points[0] = new PerspectivePoint(_points[0].X, _points[0].Y + yStep);
            _points[3] = new PerspectivePoint(_points[3].X, _points[3].Y - yStep);
        }

        if (rightInset)
        {
            _points[1] = new PerspectivePoint(_points[1].X, _points[1].Y + yStep);
            _points[2] = new PerspectivePoint(_points[2].X, _points[2].Y - yStep);
        }

        var normalized = CurrentPlan();
        _points[0] = normalized.TopLeft;
        _points[1] = normalized.TopRight;
        _points[2] = normalized.BottomRight;
        _points[3] = normalized.BottomLeft;
        Redraw();
    }

    private PerspectiveCorrectionPlan CurrentPlan()
    {
        if (_points.Count != 4)
            return PerspectiveCorrectionService.Identity(_imageWidth, _imageHeight);

        return PerspectiveCorrectionService.CreateFromCorners(
            _points[0],
            _points[1],
            _points[2],
            _points[3],
            _imageWidth,
            _imageHeight);
    }

    private void Redraw()
    {
        while (PreviewCanvas.Children.Count > 1)
            PreviewCanvas.Children.RemoveAt(1);

        if (_points.Count != 4 || _imageWidth <= 0 || _imageHeight <= 0)
        {
            ApplyButton.IsEnabled = false;
            return;
        }

        var canvasPoints = _points.Select(ToCanvasPoint).ToList();
        var polygon = new Polygon
        {
            Points = new PointCollection(canvasPoints),
            Stroke = Brushes.White,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(38, 137, 180, 250)),
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        PreviewCanvas.Children.Add(polygon);

        var labels = new[]
        {
            Strings.PerspectiveHandleTopLeft,
            Strings.PerspectiveHandleTopRight,
            Strings.PerspectiveHandleBottomRight,
            Strings.PerspectiveHandleBottomLeft
        };
        for (var i = 0; i < canvasPoints.Count; i++)
        {
            AddHandle(canvasPoints[i], labels[i], i);
        }

        var plan = CurrentPlan();
        var isIdentity = PerspectiveCorrectionService.IsIdentity(plan, _imageWidth, _imageHeight);
        ApplyButton.IsEnabled = !isIdentity;
        CornerText.Text = plan.Summary;
        StatusText.Text = isIdentity
            ? Strings.PerspectiveDragCornerStatus
            : Strings.Format("PerspectiveReadyStatusFormat", plan.OutputWidth, plan.OutputHeight);
    }

    private void AddHandle(Point point, string label, int index)
    {
        var handle = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(137, 180, 250)),
            StrokeThickness = 3,
            Cursor = Cursors.SizeAll,
            Tag = index
        };
        Canvas.SetLeft(handle, point.X - 8);
        Canvas.SetTop(handle, point.Y - 8);
        PreviewCanvas.Children.Add(handle);

        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(150, 17, 17, 27)),
            Padding = new Thickness(4, 1, 4, 2),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(text, point.X + 8);
        Canvas.SetTop(text, point.Y + 8);
        PreviewCanvas.Children.Add(text);
    }

    private int? HitTestHandle(Point position)
    {
        if (_points.Count != 4)
            return null;

        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _points.Count; i++)
        {
            var handle = ToCanvasPoint(_points[i]);
            var dx = handle.X - position.X;
            var dy = handle.Y - position.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestDistance <= 18 ? bestIndex : null;
    }

    private bool TryGetImagePoint(Point canvasPoint, out PerspectivePoint imagePoint)
    {
        imagePoint = default;
        var rect = ImageDisplayRect();
        if (rect.IsEmpty || !rect.Contains(canvasPoint))
            return false;

        var scale = CurrentScale();
        imagePoint = new PerspectivePoint(
            Math.Clamp((canvasPoint.X - rect.X) / scale, 0, Math.Max(0, _imageWidth - 1)),
            Math.Clamp((canvasPoint.Y - rect.Y) / scale, 0, Math.Max(0, _imageHeight - 1)));
        return true;
    }

    private Point ToCanvasPoint(PerspectivePoint imagePoint)
    {
        var rect = ImageDisplayRect();
        var scale = CurrentScale();
        return new Point(rect.X + imagePoint.X * scale, rect.Y + imagePoint.Y * scale);
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
        => _imageWidth <= 0 || ImageDisplayRect().IsEmpty ? 1 : ImageDisplayRect().Width / _imageWidth;

    private void ResizePreviewImage()
    {
        PreviewImage.Width = PreviewCanvas.ActualWidth;
        PreviewImage.Height = PreviewCanvas.ActualHeight;
    }

}
