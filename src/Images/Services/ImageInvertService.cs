using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Produces a colour-inverted copy of a bitmap for the non-destructive "invert colours" display
/// toggle. The source file is never touched; this only transforms the in-memory display bitmap so
/// low-contrast scans and negatives are easier to read. Alpha is preserved; RGB is inverted.
/// </summary>
public static class ImageInvertService
{
    public static BitmapSource Invert(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var bgra = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[height * stride];
        bgra.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Bgra32 layout: [B, G, R, A]. Invert colour channels, leave alpha untouched.
            pixels[i] = (byte)(255 - pixels[i]);
            pixels[i + 1] = (byte)(255 - pixels[i + 1]);
            pixels[i + 2] = (byte)(255 - pixels[i + 2]);
        }

        var inverted = BitmapSource.Create(
            width,
            height,
            bgra.DpiX <= 0 ? 96 : bgra.DpiX,
            bgra.DpiY <= 0 ? 96 : bgra.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        inverted.Freeze();
        return inverted;
    }
}
