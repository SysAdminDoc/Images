using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

public static class ImageSelectionService
{
    public static PixelSelection? Normalize(PixelSelection? selection, int pixelWidth, int pixelHeight)
    {
        if (selection is not { } value ||
            value.Width <= 0 ||
            value.Height <= 0 ||
            pixelWidth <= 0 ||
            pixelHeight <= 0)
        {
            return null;
        }

        var left = Math.Clamp(value.X, 0, pixelWidth - 1);
        var top = Math.Clamp(value.Y, 0, pixelHeight - 1);
        var right = Math.Clamp(value.X + value.Width - 1, 0, pixelWidth - 1);
        var bottom = Math.Clamp(value.Y + value.Height - 1, 0, pixelHeight - 1);

        if (right < left || bottom < top)
            return null;

        return new PixelSelection(left, top, right - left + 1, bottom - top + 1);
    }

    public static PixelSelection? CreateSelection(
        PixelCoordinate anchor,
        PixelCoordinate current,
        int pixelWidth,
        int pixelHeight)
        => Normalize(PixelInspectorService.CalculateSelection(anchor, current), pixelWidth, pixelHeight);

    public static BitmapSource ExtractSelection(BitmapSource source, PixelSelection selection)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalized = Normalize(selection, source.PixelWidth, source.PixelHeight)
            ?? throw new ArgumentOutOfRangeException(nameof(selection), "Selection is outside the image.");

        var formatted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var stride = checked(normalized.Width * 4);
        var pixels = new byte[checked(stride * normalized.Height)];
        formatted.CopyPixels(
            new Int32Rect(normalized.X, normalized.Y, normalized.Width, normalized.Height),
            pixels,
            stride,
            0);

        var bitmap = BitmapSource.Create(
            normalized.Width,
            normalized.Height,
            source.DpiX,
            source.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}
