using System.Globalization;

namespace Images.Services;

public static class CropSelectionService
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

    public static IReadOnlyDictionary<string, string> ToEditParameters(PixelSelection selection)
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = selection.X.ToString(CultureInfo.InvariantCulture),
            ["y"] = selection.Y.ToString(CultureInfo.InvariantCulture),
            ["width"] = selection.Width.ToString(CultureInfo.InvariantCulture),
            ["height"] = selection.Height.ToString(CultureInfo.InvariantCulture)
        };
}
