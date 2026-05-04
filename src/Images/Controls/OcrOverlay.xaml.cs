using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.ViewModels;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.Controls;

public partial class OcrOverlay : UserControl
{
    private static readonly ILogger _log = Log.For<OcrOverlay>();
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

    private void TextRegion_MouseDown(object sender, MouseButtonEventArgs e)
    {
        CopyRegionText(sender);
        e.Handled = true;
    }

    private void TextRegion_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        CopyRegionText(sender);
        e.Handled = true;
    }

    private void CopyRegionText(object sender)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not OcrTextLine line) return;

        try
        {
            Clipboard.SetText(line.Text);
            line.IsSelected = true;
            _ = ResetSelectionAsync(line);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clipboard copy failed: {Message}", ex.Message);
        }
    }

    private async Task ResetSelectionAsync(OcrTextLine line)
    {
        await Task.Delay(200).ConfigureAwait(true);
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        line.IsSelected = false;
    }
}
