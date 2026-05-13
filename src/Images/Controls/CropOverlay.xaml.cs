using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.Services;

namespace Images.Controls;

public partial class CropOverlay : UserControl
{
    private const double ApplyButtonGap = 8.0;
    private const double ApplyButtonMargin = 8.0;
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

    public static readonly DependencyProperty ApplyCommandProperty = DependencyProperty.Register(
        nameof(ApplyCommand),
        typeof(ICommand),
        typeof(CropOverlay),
        new PropertyMetadata(null));

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

    public ICommand? ApplyCommand
    {
        get => (ICommand?)GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
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
        var imageToViewport = Target is null
            ? Transform.Identity
            : new MatrixTransform(Target.GetImageToViewportMatrix());
        OverlayCanvas.RenderTransform = imageToViewport;

        if (Selection is not { } selection || selection.Width <= 0 || selection.Height <= 0)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            ApplyButton.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(SelectionBox, selection.X);
        Canvas.SetTop(SelectionBox, selection.Y);
        SelectionBox.Width = selection.Width;
        SelectionBox.Height = selection.Height;
        SelectionLabel.Text = selection.DisplayText;
        SelectionBox.Visibility = Visibility.Visible;
        ApplyButton.Visibility = Visibility.Visible;
        PositionApplyButton(selection, imageToViewport.Value);
    }

    private void PositionApplyButton(PixelSelection selection, Matrix imageToViewport)
    {
        ApplyButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var buttonSize = ApplyButton.DesiredSize;
        if (buttonSize.Width <= 0 || buttonSize.Height <= 0)
            return;

        var viewportWidth = ActualWidth > 0 ? ActualWidth : Target?.ActualWidth ?? 0;
        var viewportHeight = ActualHeight > 0 ? ActualHeight : Target?.ActualHeight ?? 0;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        var anchor = imageToViewport.Transform(new Point(
            selection.X + selection.Width,
            selection.Y + selection.Height));

        var left = anchor.X - buttonSize.Width;
        var top = anchor.Y + ApplyButtonGap;
        if (top + buttonSize.Height > viewportHeight - ApplyButtonMargin)
            top = anchor.Y - buttonSize.Height - ApplyButtonGap;

        left = Math.Clamp(
            left,
            ApplyButtonMargin,
            Math.Max(ApplyButtonMargin, viewportWidth - buttonSize.Width - ApplyButtonMargin));
        top = Math.Clamp(
            top,
            ApplyButtonMargin,
            Math.Max(ApplyButtonMargin, viewportHeight - buttonSize.Height - ApplyButtonMargin));

        Canvas.SetLeft(ApplyButton, left);
        Canvas.SetTop(ApplyButton, top);
    }
}
