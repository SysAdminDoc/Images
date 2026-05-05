using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Images.Controls;

public partial class LocalExposureBrushOverlay : UserControl
{
    private ZoomPanImage? _attachedTarget;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(ZoomPanImage),
        typeof(LocalExposureBrushOverlay),
        new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty StrokesProperty = DependencyProperty.Register(
        nameof(Strokes),
        typeof(IEnumerable),
        typeof(LocalExposureBrushOverlay),
        new PropertyMetadata(null));

    public ZoomPanImage? Target
    {
        get => (ZoomPanImage?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public IEnumerable? Strokes
    {
        get => (IEnumerable?)GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public LocalExposureBrushOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AttachTarget(Target);
            ApplyTargetTransform();
        };
        SizeChanged += (_, _) => ApplyTargetTransform();
        Unloaded += (_, _) => DetachTarget(Target);
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (LocalExposureBrushOverlay)d;
        overlay.DetachTarget(e.OldValue as ZoomPanImage);
        overlay.AttachTarget(e.NewValue as ZoomPanImage);
        overlay.ApplyTargetTransform();
    }

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

    private void Target_ViewChanged(object? sender, EventArgs e) => ApplyTargetTransform();

    private void ApplyTargetTransform()
    {
        OverlayCanvas.RenderTransform = Target is null
            ? Transform.Identity
            : new MatrixTransform(Target.GetImageToViewportMatrix());
    }
}
