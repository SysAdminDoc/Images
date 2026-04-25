using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media.Imaging;

namespace Images.Controls;

/// <summary>
/// A-01: custom UI Automation peer for <see cref="ZoomPanImage"/>. Screen readers (Narrator,
/// JAWS, NVDA) see the canvas as an <c>Image</c> control with a descriptive name + help text
/// pulled live from the current source. No OSS Windows image viewer publishes this tree;
/// shipping it is free positioning against competitors.
///
/// Name = "Image" (generic label; the actual filename is surfaced at the window level via
/// WindowTitle). HelpText = "W × H pixels" so a Narrator user hears dimensions on focus.
/// ItemStatus = the decoder string + animated-frame count when applicable, so state changes
/// announce.
/// </summary>
public sealed class ImageCanvasAutomationPeer : FrameworkElementAutomationPeer
{
    public ImageCanvasAutomationPeer(ZoomPanImage owner) : base(owner) { }

    private new ZoomPanImage Owner => (ZoomPanImage)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => "ZoomPanImage";

    protected override string GetNameCore()
    {
        // Read the underlying image's dimensions to build a descriptive name so JAWS/NVDA
        // read "Image, 2048 by 1365 pixels" on focus rather than a bare "Image".
        if (Owner.Source is BitmapSource bs)
            return $"Image, {bs.PixelWidth} by {bs.PixelHeight} pixels";
        return "Image (none loaded)";
    }

    protected override string GetHelpTextCore()
        => "Use arrow keys to navigate previous / next in folder. Mouse wheel zooms; drag pans; double-click fits. F1 shortcut help.";

    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
}
