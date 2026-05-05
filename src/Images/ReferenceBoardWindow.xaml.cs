using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class ReferenceBoardWindow : Window
{
    private const double MaxImageWidth = 420;
    private const double MaxImageHeight = 320;
    private const double MinGroupWidth = 280;
    private const double MinGroupHeight = 190;

    private FrameworkElement? _dragElement;
    private UIElement? _dragHandle;
    private Point _dragStart;
    private Point _dragOrigin;
    private Border? _selectedBorder;
    private int _createdItemCount;
    private int _itemCount;

    public ReferenceBoardWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = 0;
        var skipped = 0;

        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) || !SupportedImageFormats.IsSupported(path))
            {
                skipped++;
                continue;
            }

            try
            {
                AddImageCard(path);
                added++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
            {
                skipped++;
            }
        }

        if (added > 0 && skipped > 0)
            SetStatus($"Added {added} reference{Plural(added)}. Skipped {skipped} unsupported or unreadable file{Plural(skipped)}.");
        else if (added > 0)
            SetStatus($"Added {added} reference{Plural(added)}.");
        else if (skipped > 0)
            SetStatus(SupportedImageFormats.DropUnsupportedMessage);

        UpdateEmptyState();
    }

    private void AddImagesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add images to reference board",
            Filter = SupportedImageFormats.OpenDialogFilter,
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
            AddFiles(dialog.FileNames);
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        AddNoteCard();
        SetStatus("Added note card.");
        UpdateEmptyState();
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        AddGroupFrame();
        SetStatus("Added group frame. Drag the header to move it or use the corner handle to resize.");
        UpdateEmptyState();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_itemCount == 0)
        {
            SetStatus("The board is already empty.");
            return;
        }

        if (!ConfirmDialog.ConfirmReferenceBoardClear(this, _itemCount))
            return;

        BoardCanvas.Children.Clear();
        _itemCount = 0;
        _createdItemCount = 0;
        _selectedBorder = null;
        SetStatus("Board cleared.");
        UpdateEmptyState();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_itemCount == 0)
        {
            SetStatus("Add at least one image, note, or group before exporting.");
            return;
        }

        BoardCanvas.UpdateLayout();
        var bounds = ReferenceBoardLayoutService.CalculateContentBounds(GetBoardItemBounds());
        if (bounds.IsEmpty)
        {
            SetStatus("There is no visible board content to export.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export reference board",
            Filter = "PNG image|*.png",
            FileName = $"reference-board-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            AddExtension = true,
            DefaultExt = ".png",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            ExportBoard(dialog.FileName, bounds);
            SetStatus($"Exported board to {Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
    }

    private void ResetViewButton_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = 1;
        BoardScroller.ScrollToHorizontalOffset(0);
        BoardScroller.ScrollToVerticalOffset(0);
        SetStatus("Board view reset.");
    }

    private void PinCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = PinCheckBox.IsChecked == true;
        SetStatus(Topmost ? "Reference board pinned on top." : "Reference board no longer pinned.");
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BoardScale is null || ZoomText is null)
            return;

        BoardScale.ScaleX = e.NewValue;
        BoardScale.ScaleY = e.NewValue;
        ZoomText.Text = e.NewValue.ToString("P0", CultureInfo.InvariantCulture);
    }

    private void BoardScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        var delta = e.Delta > 0 ? 0.08 : -0.08;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + delta, ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void Board_DragEnter(object sender, DragEventArgs e) => ApplyDragEffects(e);

    private void Board_DragOver(object sender, DragEventArgs e) => ApplyDragEffects(e);

    private void Board_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out var files))
        {
            SetStatus(SupportedImageFormats.DropUnsupportedMessage);
            e.Handled = true;
            return;
        }

        AddFiles(files);
        e.Handled = true;
    }

    private void BoardCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SelectBorder(null);
        Keyboard.ClearFocus();
    }

    private void AddImageCard(string path)
    {
        var loaded = ImageLoader.Load(path);
        var size = FitWithin(loaded.PixelWidth, loaded.PixelHeight, MaxImageWidth, MaxImageHeight);
        var fileName = Path.GetFileName(path);
        var family = SupportedImageFormats.FormatFamily(path);

        var image = new Image
        {
            Source = loaded.Image,
            Width = size.Width,
            Height = size.Height,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var caption = new TextBlock
        {
            Text = fileName,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var detail = new TextBlock
        {
            Text = $"{loaded.PixelWidth} x {loaded.PixelHeight} - {family}",
            Foreground = Brush("SubtextBrush"),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(image);
        stack.Children.Add(caption);
        stack.Children.Add(detail);

        var card = CreateBoardBorder(
            width: Math.Max(220, size.Width + 24),
            height: size.Height + 76,
            background: Brush("PanelBrush"),
            borderBrush: Brush("HairlineBrush"),
            content: stack);

        card.ToolTip = path;
        AutomationProperties.SetName(card, $"Reference image {fileName}");
        AddBoardElement(card, draggableHandle: card, zIndex: 20);
    }

    private void AddNoteCard()
    {
        var header = CreateCardHeader("Note", "Drag note");
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            MinHeight = 110,
            Text = string.Empty,
            ToolTip = "Write a board note"
        };

        AutomationProperties.SetName(editor, "Reference board note text");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(header);
        Grid.SetRow(editor, 1);
        grid.Children.Add(editor);

        var card = CreateBoardBorder(
            width: 300,
            height: 190,
            background: Brush("Surface0Brush"),
            borderBrush: Brush("HairlineBrush"),
            content: grid);

        AutomationProperties.SetName(card, "Reference board note");
        AddBoardElement(card, draggableHandle: header, zIndex: 30);
    }

    private void AddGroupFrame()
    {
        var header = CreateCardHeader("Group", "Drag group");
        var label = new TextBox
        {
            Text = "Group",
            FontWeight = FontWeights.SemiBold,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brush("TextBrush"),
            Margin = new Thickness(14, 42, 14, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MinWidth = 160,
            ToolTip = "Rename this group"
        };

        var resizeGrip = new Thumb
        {
            Width = 18,
            Height = 18,
            Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 8),
            Background = Brush("AccentBrush"),
            BorderBrush = Brush("CrustBrush"),
            BorderThickness = new Thickness(1),
            ToolTip = "Resize group"
        };

        resizeGrip.DragDelta += (_, e) =>
        {
            if (resizeGrip.Tag is not Border group)
                return;

            group.Width = Math.Max(MinGroupWidth, group.Width + e.HorizontalChange);
            group.Height = Math.Max(MinGroupHeight, group.Height + e.VerticalChange);
            var clamped = ReferenceBoardLayoutService.ClampPosition(
                Canvas.GetLeft(group),
                Canvas.GetTop(group),
                group.Width,
                group.Height);
            Canvas.SetLeft(group, clamped.X);
            Canvas.SetTop(group, clamped.Y);
        };

        var grid = new Grid();
        grid.Children.Add(label);
        grid.Children.Add(header);
        grid.Children.Add(resizeGrip);

        var groupFrame = CreateBoardBorder(
            width: 540,
            height: 330,
            background: new SolidColorBrush(Color.FromArgb(36, 137, 180, 250)),
            borderBrush: Brush("AccentBrush"),
            content: grid);

        groupFrame.BorderThickness = new Thickness(1.5);
        resizeGrip.Tag = groupFrame;
        AutomationProperties.SetName(groupFrame, "Reference board group frame");
        AddBoardElement(groupFrame, draggableHandle: header, zIndex: 5);
    }

    private Border CreateBoardBorder(double width, double height, Brush background, Brush borderBrush, UIElement content)
    {
        return new Border
        {
            Width = width,
            Height = height,
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(10),
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Effect = (Effect?)FindResource("Elevation.Low"),
            Child = content,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
    }

    private Border CreateCardHeader(string title, string automationName)
    {
        var header = new Border
        {
            Height = 30,
            Background = new SolidColorBrush(Color.FromArgb(28, 205, 214, 244)),
            CornerRadius = new CornerRadius(7),
            Cursor = Cursors.SizeAll,
            Padding = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        header.Child = new TextBlock
        {
            Text = title,
            Foreground = Brush("SubtextBrush"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        AutomationProperties.SetName(header, automationName);
        return header;
    }

    private void AddBoardElement(Border element, UIElement draggableHandle, int zIndex)
    {
        var position = ReferenceBoardLayoutService.NextCascadedPosition(_createdItemCount);
        _createdItemCount++;
        _itemCount++;

        Canvas.SetLeft(element, position.X);
        Canvas.SetTop(element, position.Y);
        Panel.SetZIndex(element, zIndex);

        RegisterDraggable(element, draggableHandle);
        BoardCanvas.Children.Add(element);
        SelectBorder(element);
        element.Focusable = true;
        element.MouseLeftButtonDown += (_, e) =>
        {
            SelectBorder(element);
            e.Handled = false;
        };

        UpdateEmptyState();
    }

    private void RegisterDraggable(FrameworkElement element, UIElement handle)
    {
        handle.MouseLeftButtonDown += (_, e) =>
        {
            SelectBorder(element as Border);
            _dragElement = element;
            _dragHandle = handle;
            _dragStart = e.GetPosition(BoardCanvas);
            _dragOrigin = new Point(Canvas.GetLeft(element), Canvas.GetTop(element));
            handle.CaptureMouse();
            e.Handled = true;
        };

        handle.MouseMove += (_, e) =>
        {
            if (_dragElement is null || _dragHandle is null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var current = e.GetPosition(BoardCanvas);
            var desiredLeft = _dragOrigin.X + (current.X - _dragStart.X);
            var desiredTop = _dragOrigin.Y + (current.Y - _dragStart.Y);
            var clamped = ReferenceBoardLayoutService.ClampPosition(
                desiredLeft,
                desiredTop,
                _dragElement.ActualWidth > 0 ? _dragElement.ActualWidth : _dragElement.Width,
                _dragElement.ActualHeight > 0 ? _dragElement.ActualHeight : _dragElement.Height);

            Canvas.SetLeft(_dragElement, clamped.X);
            Canvas.SetTop(_dragElement, clamped.Y);
            e.Handled = true;
        };

        handle.MouseLeftButtonUp += (_, e) =>
        {
            if (_dragHandle is not null && _dragHandle.IsMouseCaptured)
                _dragHandle.ReleaseMouseCapture();

            _dragElement = null;
            _dragHandle = null;
            e.Handled = true;
        };
    }

    private IEnumerable<ReferenceBoardItemBounds> GetBoardItemBounds()
    {
        foreach (UIElement child in BoardCanvas.Children)
        {
            if (child is not FrameworkElement element)
                continue;

            var width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
            var height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;
            yield return new ReferenceBoardItemBounds(
                Canvas.GetLeft(element),
                Canvas.GetTop(element),
                width,
                height);
        }
    }

    private void ExportBoard(string path, Rect bounds)
    {
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height));

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brush("ViewportBrush"), null, new Rect(0, 0, pixelWidth, pixelHeight));
            context.PushTransform(new TranslateTransform(-bounds.Left, -bounds.Top));
            context.DrawRectangle(
                new VisualBrush(BoardCanvas)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                },
                null,
                new Rect(0, 0, BoardCanvas.Width, BoardCanvas.Height));
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private void ApplyDragEffects(DragEventArgs e)
    {
        e.Effects = TryGetDroppedFiles(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryGetDroppedFiles(DragEventArgs e, out string[] files)
    {
        files = [];

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        files = ((string[]?)e.Data.GetData(DataFormats.FileDrop) ?? [])
            .Where(File.Exists)
            .ToArray();

        return files.Any(SupportedImageFormats.IsSupported);
    }

    private void SelectBorder(Border? border)
    {
        if (_selectedBorder is not null)
            _selectedBorder.BorderBrush = _selectedBorder.Tag as Brush ?? Brush("HairlineBrush");

        _selectedBorder = border;

        if (_selectedBorder is null)
            return;

        _selectedBorder.Tag ??= _selectedBorder.BorderBrush;
        _selectedBorder.BorderBrush = Brush("AccentBrush");
        _selectedBorder.Focus();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _itemCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);

    private static Size FitWithin(double width, double height, double maxWidth, double maxHeight)
    {
        if (width <= 0 || height <= 0)
            return new Size(maxWidth, maxHeight);

        var scale = Math.Min(maxWidth / width, maxHeight / height);
        scale = Math.Min(1, scale);
        return new Size(Math.Max(1, width * scale), Math.Max(1, height * scale));
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
