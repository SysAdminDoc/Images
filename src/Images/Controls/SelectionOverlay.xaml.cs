using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.Services;

namespace Images.Controls;

public partial class SelectionOverlay : UserControl
{
    private const double ActionPanelGap = 8.0;
    private const double ActionPanelMargin = 8.0;
    private ZoomPanImage? _attachedTarget;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(ZoomPanImage),
        typeof(SelectionOverlay),
        new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection),
        typeof(PixelSelection?),
        typeof(SelectionOverlay),
        new PropertyMetadata(null, OnSelectionChanged));

    public static readonly DependencyProperty CopyCommandProperty = DependencyProperty.Register(
        nameof(CopyCommand),
        typeof(ICommand),
        typeof(SelectionOverlay),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.Register(
        nameof(ClearCommand),
        typeof(ICommand),
        typeof(SelectionOverlay),
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

    public ICommand? CopyCommand
    {
        get => (ICommand?)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public ICommand? ClearCommand
    {
        get => (ICommand?)GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    public SelectionOverlay()
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
        var overlay = (SelectionOverlay)d;
        overlay.DetachTarget(e.OldValue as ZoomPanImage);
        overlay.AttachTarget(e.NewValue as ZoomPanImage);
        overlay.ApplyOverlayState();
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SelectionOverlay)d).ApplyOverlayState();

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
            ActionPanel.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(SelectionBox, selection.X);
        Canvas.SetTop(SelectionBox, selection.Y);
        SelectionBox.Width = selection.Width;
        SelectionBox.Height = selection.Height;
        SelectionLabel.Text = selection.DisplayText;
        SelectionBox.Visibility = Visibility.Visible;
        ActionPanel.Visibility = Visibility.Visible;
        PositionActionPanel(selection, imageToViewport.Value);
    }

    private void PositionActionPanel(PixelSelection selection, Matrix imageToViewport)
    {
        ActionPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var panelSize = ActionPanel.DesiredSize;
        if (panelSize.Width <= 0 || panelSize.Height <= 0)
            return;

        var viewportWidth = ActualWidth > 0 ? ActualWidth : Target?.ActualWidth ?? 0;
        var viewportHeight = ActualHeight > 0 ? ActualHeight : Target?.ActualHeight ?? 0;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        var anchor = imageToViewport.Transform(new Point(
            selection.X + selection.Width,
            selection.Y + selection.Height));

        var left = anchor.X - panelSize.Width;
        var top = anchor.Y + ActionPanelGap;
        if (top + panelSize.Height > viewportHeight - ActionPanelMargin)
            top = anchor.Y - panelSize.Height - ActionPanelGap;

        left = Math.Clamp(
            left,
            ActionPanelMargin,
            Math.Max(ActionPanelMargin, viewportWidth - panelSize.Width - ActionPanelMargin));
        top = Math.Clamp(
            top,
            ActionPanelMargin,
            Math.Max(ActionPanelMargin, viewportHeight - panelSize.Height - ActionPanelMargin));

        Canvas.SetLeft(ActionPanel, left);
        Canvas.SetTop(ActionPanel, top);
    }
}
