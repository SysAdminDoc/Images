using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Produces a grayscale <see cref="BitmapSource"/> representing a single color channel
/// of the input image. The result is always frozen and safe to use on the UI thread.
/// </summary>
public static class ChannelIsolationService
{
    /// <summary>
    /// Returns a channel-isolated copy, or the original source when <paramref name="mode"/>
    /// is <see cref="ChannelMode.Normal"/>.
    /// </summary>
    public static BitmapSource? Isolate(BitmapSource? source, ChannelMode mode)
    {
        if (source is null || mode == ChannelMode.Normal)
            return source;

        // Convert to Bgra32 for consistent pixel access regardless of the source format.
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        converted.CopyPixels(pixels, stride, 0);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            byte a = pixels[i + 3];

            byte val = mode switch
            {
                ChannelMode.Red => r,
                ChannelMode.Green => g,
                ChannelMode.Blue => b,
                ChannelMode.Alpha => a,
                _ => 0
            };

            // Render as grayscale of the isolated channel, fully opaque for display.
            pixels[i] = val;       // B
            pixels[i + 1] = val;   // G
            pixels[i + 2] = val;   // R
            pixels[i + 3] = 255;   // A
        }

        var result = BitmapSource.Create(w, h, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}
