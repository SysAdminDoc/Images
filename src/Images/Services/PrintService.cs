using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// V15-10: prints the current image via <see cref="PrintDialog"/>. Single page, fit-to-page
/// with margin, orientation derived from aspect ratio. No layout options this version — user's
/// in the system print dialog for paper + DPI selection.
/// </summary>
public static class PrintService
{
    private const double MarginInches = 0.5;
    private const double DIU_PER_INCH = 96.0; // WPF device-independent units per inch

    /// <summary>
    /// Prints <paramref name="source"/> through the system print dialog. Returns true if the
    /// user confirmed and the job was queued; false if they cancelled. Exceptions surface to the
    /// caller so the viewer's toast-on-failure path stays in charge.
    /// </summary>
    public static bool Print(BitmapSource source, string documentTitle)
    {
        ArgumentNullException.ThrowIfNull(source);

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return false;

        var pageWidth = dlg.PrintableAreaWidth;
        var pageHeight = dlg.PrintableAreaHeight;
        var marginDiu = MarginInches * DIU_PER_INCH;

        var contentW = Math.Max(1.0, pageWidth - marginDiu * 2);
        var contentH = Math.Max(1.0, pageHeight - marginDiu * 2);

        // Scale-to-fit inside margins; never upscale past 1:1 (printing a small PNG full-bleed
        // would look absurd — respect the user's pixel density).
        var fitScale = Math.Min(contentW / source.PixelWidth, contentH / source.PixelHeight);
        if (fitScale > 1.0) fitScale = 1.0;

        var renderedW = source.PixelWidth * fitScale;
        var renderedH = source.PixelHeight * fitScale;

        var image = new Image
        {
            Source = source,
            Width = renderedW,
            Height = renderedH,
            Stretch = Stretch.Uniform,
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        // Center the image on the page.
        var canvas = new System.Windows.Controls.Canvas
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
        };
        System.Windows.Controls.Canvas.SetLeft(image, (pageWidth - renderedW) / 2);
        System.Windows.Controls.Canvas.SetTop(image, (pageHeight - renderedH) / 2);
        canvas.Children.Add(image);

        canvas.Measure(new Size(pageWidth, pageHeight));
        canvas.Arrange(new Rect(0, 0, pageWidth, pageHeight));

        dlg.PrintVisual(canvas, documentTitle);
        return true;
    }
}
