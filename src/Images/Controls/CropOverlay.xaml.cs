using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;

namespace Images.Controls;

public partial class CropOverlay : UserControl
{
    private ZoomPanImage? _attachedTarget;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(ZoomPanImage),
        typeof(CropOverlay),
        new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection),
        typeof(PixelSelection?),
        typeof(CropOverlay),
        new PropertyMetadata(null, OnSelectionChanged));

    public ZoomPanImage? Target
    {
        get => (ZoomPanImage?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public PixelSelection? Selection
    {
        get => (PixelSelection?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public CropOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AttachTarget(Target);
            ApplyOverlayState();
        };
        SizeChanged += (_, _) => ApplyOverlayState();
        Unloaded += (_, _) => DetachTarget(Target);
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (CropOverlay)d;
        overlay.DetachTarget(e.OldValue as ZoomPanImage);
        overlay.AttachTarget(e.NewValue as ZoomPanImage);
        overlay.ApplyOverlayState();
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CropOverlay)d).ApplyOverlayState();

    private void AttachTarget(ZoomPanImage? target)
    {
        if (target is null || ReferenceEquals(_attachedTarget, target))
            return;

        DetachTarget(_attachedTarget);
        target.ViewChanged += Target_ViewChanged;
        _attachedTarget = target;
    }

    private void DetachTarget(ZoomPanImage? target)
    {
        if (target is null || !ReferenceEquals(_attachedTarget, target))
            return;

        target.ViewChanged -= Target_ViewChanged;
        _attachedTarget = null;
    }

    private void Target_ViewChanged(object? sender, EventArgs e) => ApplyOverlayState();

    private void ApplyOverlayState()
    {
        OverlayCanvas.RenderTransform = Target is null
            ? Transform.Identity
            : new MatrixTransform(Target.GetImageToViewportMatrix());

        if (Selection is not { } selection || selection.Width <= 0 || selection.Height <= 0)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(SelectionBox, selection.X);
        Canvas.SetTop(SelectionBox, selection.Y);
        SelectionBox.Width = selection.Width;
        SelectionBox.Height = selection.Height;
        SelectionLabel.Text = selection.DisplayText;
        SelectionBox.Visibility = Visibility.Visible;
    }
}
