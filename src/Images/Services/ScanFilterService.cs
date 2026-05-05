using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Preview-only scan cleanup helpers. These transforms are applied to the displayed bitmap only;
/// source files, archive entries, export paths, and metadata are left untouched.
/// </summary>
public static class ScanFilterService
{
    public static BitmapSource ApplyOldScanFilter(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            return source;

        var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        formatted.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];

            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            var normalized = (luminance - 18.0) * 255.0 / 217.0;
            var boosted = (normalized - 128.0) * 1.22 + 128.0;
            var gray = (byte)Math.Clamp((int)Math.Round(boosted), 0, 255);

            pixels[i] = gray;
            pixels[i + 1] = gray;
            pixels[i + 2] = gray;
        }

        var filtered = BitmapSource.Create(
            source.PixelWidth,
            source.PixelHeight,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        filtered.Freeze();
        return filtered;
    }
}
