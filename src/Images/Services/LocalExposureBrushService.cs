using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;

namespace Images.Services;

public sealed record LocalExposureBrushStroke(double X, double Y, double Radius, double Strength)
{
    public double Left => X - Radius;
    public double Top => Y - Radius;
    public double Diameter => Radius * 2;
    public bool IsDodge => Strength >= 0;
    public string ModeLabel => IsDodge ? "Dodge" : "Burn";
}

public static class LocalExposureBrushService
{
    public const double MinRadius = 4;
    public const double MaxRadius = 320;
    public const double MinStrength = -1;
    public const double MaxStrength = 1;
    private const string StrokesKey = "strokes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static LocalExposureBrushStroke NormalizeStroke(
        PixelCoordinate center,
        double radius,
        double strength,
        int imageWidth,
        int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image dimensions must be positive.");

        return new LocalExposureBrushStroke(
            Math.Clamp(center.X, 0, imageWidth - 1),
            Math.Clamp(center.Y, 0, imageHeight - 1),
            Math.Clamp(double.IsFinite(radius) ? radius : 48, MinRadius, MaxRadius),
            Math.Clamp(double.IsFinite(strength) ? strength : 0.28, MinStrength, MaxStrength));
    }

    public static IReadOnlyDictionary<string, string> ToEditParameters(IEnumerable<LocalExposureBrushStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var normalized = strokes
            .Where(stroke => stroke.Radius > 0 && Math.Abs(stroke.Strength) > 0.0001)
            .Select(stroke => new LocalExposureBrushStroke(
                Math.Max(0, stroke.X),
                Math.Max(0, stroke.Y),
                Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                Math.Clamp(stroke.Strength, MinStrength, MaxStrength)))
            .ToList();

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StrokesKey] = JsonSerializer.Serialize(normalized, JsonOptions)
        };
    }

    public static IReadOnlyList<LocalExposureBrushStroke> FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(StrokesKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<LocalExposureBrushStroke>>(raw, JsonOptions)
                ?.Where(stroke => stroke.Radius > 0 && Math.Abs(stroke.Strength) > 0.0001)
                .Select(stroke => new LocalExposureBrushStroke(
                    Math.Max(0, stroke.X),
                    Math.Max(0, stroke.Y),
                    Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                    Math.Clamp(stroke.Strength, MinStrength, MaxStrength)))
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string CreateLabel(IReadOnlyList<LocalExposureBrushStroke> strokes)
    {
        if (strokes.Count == 0)
            return "Local exposure";

        var dodge = strokes.Count(stroke => stroke.Strength > 0);
        var burn = strokes.Count(stroke => stroke.Strength < 0);
        return (dodge, burn) switch
        {
            (> 0, > 0) => $"Local exposure ({dodge} dodge, {burn} burn)",
            (> 0, 0) => $"Dodge brush ({dodge} stroke{Plural(dodge)})",
            (0, > 0) => $"Burn brush ({burn} stroke{Plural(burn)})",
            _ => "Local exposure"
        };
    }

    public static string CreateSummary(IReadOnlyList<LocalExposureBrushStroke> strokes)
    {
        if (strokes.Count == 0)
            return "No brush strokes";

        var averageRadius = strokes.Average(stroke => stroke.Radius);
        var averageStrength = strokes.Average(stroke => Math.Abs(stroke.Strength));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{strokes.Count} stroke{Plural(strokes.Count)}, radius {averageRadius:0.#} px, strength {averageStrength:P0}");
    }

    public static void Apply(MagickImage image, IEnumerable<LocalExposureBrushStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(strokes);

        var maxX = Math.Max(0, (int)image.Width - 1);
        var maxY = Math.Max(0, (int)image.Height - 1);
        var normalized = strokes
            .Where(stroke => stroke.Radius > 0 && Math.Abs(stroke.Strength) > 0.0001)
            .Select(stroke => new LocalExposureBrushStroke(
                Math.Clamp(stroke.X, 0, maxX),
                Math.Clamp(stroke.Y, 0, maxY),
                Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                Math.Clamp(stroke.Strength, MinStrength, MaxStrength)))
            .ToList();

        if (normalized.Count == 0 || image.Width == 0 || image.Height == 0)
            return;

        var pixels = image.GetPixels().ToByteArray(PixelMapping.RGBA);
        if (pixels is null || pixels.Length == 0)
            return;

        var width = checked((int)image.Width);
        var height = checked((int)image.Height);

        foreach (var stroke in normalized)
        {
            ApplyStroke(pixels, width, height, stroke);
        }

        image.ImportPixels(pixels, new PixelImportSettings(image.Width, image.Height, StorageType.Char, PixelMapping.RGBA));
    }

    private static void ApplyStroke(byte[] pixels, int width, int height, LocalExposureBrushStroke stroke)
    {
        var radius = Math.Max(MinRadius, stroke.Radius);
        var radiusSquared = radius * radius;
        var left = Math.Max(0, (int)Math.Floor(stroke.X - radius));
        var top = Math.Max(0, (int)Math.Floor(stroke.Y - radius));
        var right = Math.Min(width - 1, (int)Math.Ceiling(stroke.X + radius));
        var bottom = Math.Min(height - 1, (int)Math.Ceiling(stroke.Y + radius));

        for (var y = top; y <= bottom; y++)
        {
            var dy = y - stroke.Y;
            for (var x = left; x <= right; x++)
            {
                var dx = x - stroke.X;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared)
                    continue;

                var distance = Math.Sqrt(distanceSquared);
                var falloff = 0.5 + 0.5 * Math.Cos(Math.PI * distance / radius);
                var amount = stroke.Strength * falloff;
                var index = ((y * width) + x) * 4;
                pixels[index] = AdjustChannel(pixels[index], amount);
                pixels[index + 1] = AdjustChannel(pixels[index + 1], amount);
                pixels[index + 2] = AdjustChannel(pixels[index + 2], amount);
            }
        }
    }

    private static byte AdjustChannel(byte channel, double amount)
    {
        var adjusted = amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);
        return (byte)Math.Clamp((int)Math.Round(adjusted, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static string Plural(int count) => count == 1 ? "" : "s";
}
