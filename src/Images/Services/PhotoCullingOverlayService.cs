using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

[Flags]
public enum PhotoCullingOverlayMode
{
    None = 0,
    FocusPeaking = 1,
    ExposureClipping = 2,
}

/// <summary>
/// Builds transparent, display-only analysis overlays from the decoded image buffer. The work is
/// intentionally bounded so enabling an overlay on a large RAW file cannot allocate an
/// unbounded full-resolution analysis surface.
/// </summary>
public static class PhotoCullingOverlayService
{
    internal const int HighlightThreshold = 250;
    internal const int ShadowThreshold = 5;
    internal const int FocusGradientThreshold = 220;
    internal const int MaxAnalysisEdge = 4096;

    public static BitmapSource CreateOverlay(
        BitmapSource source,
        PhotoCullingOverlayMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (mode == PhotoCullingOverlayMode.None)
            throw new ArgumentOutOfRangeException(nameof(mode), "At least one overlay mode is required.");

        cancellationToken.ThrowIfCancellationRequested();
        var working = DownsampleForAnalysis(source);
        var converted = working.Format == PixelFormats.Bgra32
            ? working
            : new FormatConvertedBitmap(working, PixelFormats.Bgra32, null, 0);
        if (converted.CanFreeze && !converted.IsFrozen)
            converted.Freeze();

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = checked(width * 4);
        var sourcePixels = new byte[checked(stride * height)];
        converted.CopyPixels(sourcePixels, stride, 0);

        var overlayPixels = AnalyzeBgra32(sourcePixels, width, height, stride, mode, cancellationToken);
        var overlay = BitmapSource.Create(
            width,
            height,
            converted.DpiX > 0 ? converted.DpiX : 96,
            converted.DpiY > 0 ? converted.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            overlayPixels,
            stride);
        overlay.Freeze();
        return overlay;
    }

    internal static byte[] AnalyzeBgra32(
        ReadOnlySpan<byte> source,
        int width,
        int height,
        int stride,
        PhotoCullingOverlayMode mode,
        CancellationToken cancellationToken = default)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (stride < checked(width * 4) || source.Length < checked(stride * height))
            throw new ArgumentException("The BGRA buffer is smaller than the declared image.", nameof(source));
        if (mode == PhotoCullingOverlayMode.None)
            throw new ArgumentOutOfRangeException(nameof(mode));

        var output = new byte[checked(stride * height)];
        var showFocus = mode.HasFlag(PhotoCullingOverlayMode.FocusPeaking);
        var showClipping = mode.HasFlag(PhotoCullingOverlayMode.ExposureClipping);

        for (var y = 0; y < height; y++)
        {
            if ((y & 63) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                if (source[offset + 3] == 0)
                    continue;

                var luminance = Luminance(source, offset);
                if (showClipping && luminance >= HighlightThreshold)
                {
                    SetColor(output, offset, blue: 62, green: 62, red: 255, alpha: 220);
                    continue;
                }

                if (showClipping && luminance <= ShadowThreshold)
                {
                    SetColor(output, offset, blue: 255, green: 142, red: 48, alpha: 220);
                    continue;
                }

                if (!showFocus || x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    continue;

                var northWest = Luminance(source, ((y - 1) * stride) + ((x - 1) * 4));
                var north = Luminance(source, ((y - 1) * stride) + (x * 4));
                var northEast = Luminance(source, ((y - 1) * stride) + ((x + 1) * 4));
                var west = Luminance(source, (y * stride) + ((x - 1) * 4));
                var east = Luminance(source, (y * stride) + ((x + 1) * 4));
                var southWest = Luminance(source, ((y + 1) * stride) + ((x - 1) * 4));
                var south = Luminance(source, ((y + 1) * stride) + (x * 4));
                var southEast = Luminance(source, ((y + 1) * stride) + ((x + 1) * 4));

                var gradientX = -northWest + northEast - (2 * west) + (2 * east) - southWest + southEast;
                var gradientY = -northWest - (2 * north) - northEast + southWest + (2 * south) + southEast;
                if (Math.Abs(gradientX) + Math.Abs(gradientY) >= FocusGradientThreshold)
                    SetColor(output, offset, blue: 76, green: 255, red: 76, alpha: 220);
            }
        }

        return output;
    }

    private static BitmapSource DownsampleForAnalysis(BitmapSource source)
    {
        var largestEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largestEdge <= MaxAnalysisEdge)
            return source;

        var scale = MaxAnalysisEdge / (double)largestEdge;
        var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static int Luminance(ReadOnlySpan<byte> pixels, int offset)
        => ((pixels[offset + 2] * 54) + (pixels[offset + 1] * 183) + (pixels[offset] * 19)) >> 8;

    private static void SetColor(byte[] output, int offset, byte blue, byte green, byte red, byte alpha)
    {
        output[offset] = blue;
        output[offset + 1] = green;
        output[offset + 2] = red;
        output[offset + 3] = alpha;
    }
}
