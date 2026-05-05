using System.Globalization;

namespace Images.Services;

public sealed record CropAspectPreset(
    string Id,
    string Label,
    string Description,
    int WidthRatio,
    int HeightRatio)
{
    public bool IsFree => WidthRatio <= 0 || HeightRatio <= 0;
    public double Ratio => IsFree ? 0 : WidthRatio / (double)HeightRatio;
}

public static class CropSelectionService
{
    public const string FreeAspectPresetId = "free";
    public const string CustomAspectPresetId = "custom";

    public static readonly CropAspectPreset FreeAspectPreset = new(
        FreeAspectPresetId,
        "Free",
        "Drag any width and height.",
        0,
        0);

    public static readonly CropAspectPreset CustomAspectPreset = new(
        CustomAspectPresetId,
        "Custom",
        "Use the custom width and height fields.",
        0,
        0);

    public static readonly IReadOnlyList<CropAspectPreset> AspectPresets =
    [
        FreeAspectPreset,
        new("square", "1:1", "Square crop.", 1, 1),
        new("3x2", "3:2", "Classic photo print crop.", 3, 2),
        new("4x3", "4:3", "Standard camera crop.", 4, 3),
        new("16x9", "16:9", "Wide screen crop.", 16, 9),
        CustomAspectPreset
    ];

    public static CropAspectPreset? FindAspectPreset(string? id)
        => AspectPresets.FirstOrDefault(preset => preset.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static PixelSelection? CreateSelection(
        PixelCoordinate anchor,
        PixelCoordinate current,
        CropAspectPreset aspect,
        int pixelWidth,
        int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return null;

        if (aspect.IsFree)
            return Normalize(PixelInspectorService.CalculateSelection(anchor, current), pixelWidth, pixelHeight);

        var xDirection = current.X >= anchor.X ? 1 : -1;
        var yDirection = current.Y >= anchor.Y ? 1 : -1;
        var dragWidth = Math.Abs(current.X - anchor.X) + 1;
        var dragHeight = Math.Abs(current.Y - anchor.Y) + 1;
        var maxWidth = xDirection > 0 ? pixelWidth - anchor.X : anchor.X + 1;
        var maxHeight = yDirection > 0 ? pixelHeight - anchor.Y : anchor.Y + 1;

        var (width, height) = FitAspectInsideDrag(dragWidth, dragHeight, maxWidth, maxHeight, aspect.Ratio);
        var endX = anchor.X + (width - 1) * xDirection;
        var endY = anchor.Y + (height - 1) * yDirection;
        return Normalize(PixelInspectorService.CalculateSelection(anchor, new PixelCoordinate(endX, endY)), pixelWidth, pixelHeight);
    }

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

    private static (int Width, int Height) FitAspectInsideDrag(
        int dragWidth,
        int dragHeight,
        int maxWidth,
        int maxHeight,
        double ratio)
    {
        if (ratio <= 0 || maxWidth <= 0 || maxHeight <= 0)
            return (0, 0);

        var width = Math.Max(1, dragWidth);
        var height = Math.Max(1, dragHeight);
        if (width / (double)height > ratio)
            width = Math.Max(1, (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero));
        else
            height = Math.Max(1, (int)Math.Round(width / ratio, MidpointRounding.AwayFromZero));

        for (var i = 0; i < 4; i++)
        {
            if (width > maxWidth)
            {
                width = maxWidth;
                height = Math.Max(1, (int)Math.Round(width / ratio, MidpointRounding.AwayFromZero));
            }

            if (height > maxHeight)
            {
                height = maxHeight;
                width = Math.Max(1, (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero));
            }

            width = Math.Clamp(width, 1, maxWidth);
            height = Math.Clamp(height, 1, maxHeight);
        }

        return (width, height);
    }
}
