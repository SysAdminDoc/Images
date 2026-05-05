using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

public readonly record struct PixelCoordinate(int X, int Y);

public readonly record struct PixelSelection(int X, int Y, int Width, int Height)
{
    public string DisplayText => $"{Width} x {Height} px at {X}, {Y}";
}

public sealed record PixelSample(
    PixelCoordinate Coordinate,
    byte R,
    byte G,
    byte B,
    byte A,
    string Hex,
    string Rgb,
    string Hsv,
    string Alpha)
{
    public string CoordinateText => $"x {Coordinate.X}, y {Coordinate.Y}";
    public string Summary => $"{CoordinateText} - {Hex} - {Rgb} - {Hsv}";
}

public static class PixelInspectorService
{
    public static bool TryMapViewportPointToPixel(
        Matrix imageToViewport,
        Point viewportPoint,
        int pixelWidth,
        int pixelHeight,
        out PixelCoordinate coordinate)
    {
        coordinate = default;

        if (pixelWidth <= 0 || pixelHeight <= 0 || !imageToViewport.HasInverse)
            return false;

        var inverse = imageToViewport;
        inverse.Invert();
        var imagePoint = inverse.Transform(viewportPoint);
        return TryCreateCoordinate(imagePoint.X, imagePoint.Y, pixelWidth, pixelHeight, out coordinate);
    }

    public static bool TryMapElementPointToPixel(
        Point elementPoint,
        double elementWidth,
        double elementHeight,
        int pixelWidth,
        int pixelHeight,
        out PixelCoordinate coordinate)
    {
        coordinate = default;

        if (elementWidth <= 0 || elementHeight <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            return false;

        var x = elementPoint.X / elementWidth * pixelWidth;
        var y = elementPoint.Y / elementHeight * pixelHeight;
        return TryCreateCoordinate(x, y, pixelWidth, pixelHeight, out coordinate);
    }

    public static PixelSelection CalculateSelection(PixelCoordinate start, PixelCoordinate end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        return new PixelSelection(
            left,
            top,
            right - left + 1,
            bottom - top + 1);
    }

    public static PixelSample SamplePixel(BitmapSource source, PixelCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (coordinate.X < 0 || coordinate.Y < 0 ||
            coordinate.X >= source.PixelWidth || coordinate.Y >= source.PixelHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Pixel coordinate is outside the image.");
        }

        var formatted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var pixels = new byte[4];
        formatted.CopyPixels(new Int32Rect(coordinate.X, coordinate.Y, 1, 1), pixels, 4, 0);

        var b = pixels[0];
        var g = pixels[1];
        var r = pixels[2];
        var a = pixels[3];
        var hex = $"#{r:X2}{g:X2}{b:X2}";
        var rgb = $"RGB {r}, {g}, {b}";
        var hsv = FormatHsv(r, g, b);
        var alpha = $"A {a}";

        return new PixelSample(coordinate, r, g, b, a, hex, rgb, hsv, alpha);
    }

    private static bool TryCreateCoordinate(
        double x,
        double y,
        int pixelWidth,
        int pixelHeight,
        out PixelCoordinate coordinate)
    {
        coordinate = default;

        if (double.IsNaN(x) || double.IsNaN(y) ||
            double.IsInfinity(x) || double.IsInfinity(y) ||
            x < 0 || y < 0 ||
            x >= pixelWidth || y >= pixelHeight)
        {
            return false;
        }

        coordinate = new PixelCoordinate((int)Math.Floor(x), (int)Math.Floor(y));
        return true;
    }

    private static string FormatHsv(byte r, byte g, byte b)
    {
        var red = r / 255.0;
        var green = g / 255.0;
        var blue = b / 255.0;

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        double hue;
        if (delta == 0)
            hue = 0;
        else if (max == red)
            hue = 60 * (((green - blue) / delta) % 6);
        else if (max == green)
            hue = 60 * (((blue - red) / delta) + 2);
        else
            hue = 60 * (((red - green) / delta) + 4);

        if (hue < 0)
            hue += 360;

        var saturation = max == 0 ? 0 : delta / max;
        return string.Create(CultureInfo.InvariantCulture, $"HSV {hue:0}, {saturation * 100:0}%, {max * 100:0}%");
    }
}
