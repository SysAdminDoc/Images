using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Images.Controls;

public partial class OcrOverlay : UserControl
{
    private ZoomPanImage? _attachedTarget;

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(ZoomPanImage),
        typeof(OcrOverlay),
        new PropertyMetadata(null, OnTargetChanged));

    public ZoomPanImage? Target
    {
        get => (ZoomPanImage?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public OcrOverlay()
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
        var overlay = (OcrOverlay)d;
        overlay.DetachTarget(e.OldValue as ZoomPanImage);
        overlay.AttachTarget(e.NewValue as ZoomPanImage);
        overlay.ApplyTargetTransform();
    }

    private void AttachTarget(ZoomPanImage? target)
    {
        if (target is null) return;
        if (ReferenceEquals(_attachedTarget, target)) return;
        DetachTarget(_attachedTarget);
        target.ViewChanged += Target_ViewChanged;
        _attachedTarget = target;
    }

    private void DetachTarget(ZoomPanImage? target)
    {
        if (target is null) return;
        if (!ReferenceEquals(_attachedTarget, target)) return;
        target.ViewChanged -= Target_ViewChanged;
        _attachedTarget = null;
    }

    private void Target_ViewChanged(object? sender, EventArgs e) => ApplyTargetTransform();

    private void ApplyTargetTransform()
    {
        OverlayItems.RenderTransform = Target is null
            ? Transform.Identity
            : new MatrixTransform(Target.GetImageToViewportMatrix());
    }
}
