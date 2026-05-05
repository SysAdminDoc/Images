using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;

namespace Images.Services;

public sealed record RedEyeCorrectionMark(double X, double Y, double Radius, double Strength, double Threshold)
{
    public double Left => X - Radius;
    public double Top => Y - Radius;
    public double Diameter => Radius * 2;
}

public static class RedEyeCorrectionService
{
    public const double MinRadius = 4;
    public const double MaxRadius = 180;
    public const double MinStrength = 0.05;
    public const double MaxStrength = 1;
    public const double MinThreshold = 0;
    public const double MaxThreshold = 1;
    private const string MarksKey = "marks";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RedEyeCorrectionMark NormalizeMark(
        PixelCoordinate center,
        double radius,
        double strength,
        double threshold,
        int imageWidth,
        int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image dimensions must be positive.");

        return new RedEyeCorrectionMark(
            Math.Clamp(center.X, 0, imageWidth - 1),
            Math.Clamp(center.Y, 0, imageHeight - 1),
            Math.Clamp(double.IsFinite(radius) ? radius : 22, MinRadius, MaxRadius),
            Math.Clamp(double.IsFinite(strength) ? strength : 0.85, MinStrength, MaxStrength),
            Math.Clamp(double.IsFinite(threshold) ? threshold : 0.35, MinThreshold, MaxThreshold));
    }

    public static IReadOnlyDictionary<string, string> ToEditParameters(IEnumerable<RedEyeCorrectionMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);
        var normalized = marks
            .Where(mark => mark.Radius > 0 && mark.Strength > 0)
            .Select(mark => new RedEyeCorrectionMark(
                Math.Max(0, mark.X),
                Math.Max(0, mark.Y),
                Math.Clamp(mark.Radius, MinRadius, MaxRadius),
                Math.Clamp(mark.Strength, MinStrength, MaxStrength),
                Math.Clamp(mark.Threshold, MinThreshold, MaxThreshold)))
            .ToList();

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MarksKey] = JsonSerializer.Serialize(normalized, JsonOptions)
        };
    }

    public static IReadOnlyList<RedEyeCorrectionMark> FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(MarksKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<RedEyeCorrectionMark>>(raw, JsonOptions)
                ?.Where(mark => mark.Radius > 0 && mark.Strength > 0)
                .Select(mark => new RedEyeCorrectionMark(
                    Math.Max(0, mark.X),
                    Math.Max(0, mark.Y),
                    Math.Clamp(mark.Radius, MinRadius, MaxRadius),
                    Math.Clamp(mark.Strength, MinStrength, MaxStrength),
                    Math.Clamp(mark.Threshold, MinThreshold, MaxThreshold)))
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string CreateLabel(IReadOnlyList<RedEyeCorrectionMark> marks)
    {
        if (marks.Count == 0)
            return "Red-eye correction";

        return $"Red-eye correction ({marks.Count} mark{Plural(marks.Count)})";
    }

    public static string CreateSummary(IReadOnlyList<RedEyeCorrectionMark> marks)
    {
        if (marks.Count == 0)
            return "No correction marks";

        var averageRadius = marks.Average(mark => mark.Radius);
        var averageStrength = marks.Average(mark => mark.Strength);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{marks.Count} mark{Plural(marks.Count)}, radius {averageRadius:0.#} px, strength {averageStrength:P0}");
    }

    public static void Apply(MagickImage image, IEnumerable<RedEyeCorrectionMark> marks)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(marks);

        var maxX = Math.Max(0, (int)image.Width - 1);
        var maxY = Math.Max(0, (int)image.Height - 1);
        var normalized = marks
            .Where(mark => mark.Radius > 0 && mark.Strength > 0)
            .Select(mark => new RedEyeCorrectionMark(
                Math.Clamp(mark.X, 0, maxX),
                Math.Clamp(mark.Y, 0, maxY),
                Math.Clamp(mark.Radius, MinRadius, MaxRadius),
                Math.Clamp(mark.Strength, MinStrength, MaxStrength),
                Math.Clamp(mark.Threshold, MinThreshold, MaxThreshold)))
            .ToList();

        if (normalized.Count == 0 || image.Width == 0 || image.Height == 0)
            return;

        var pixels = image.GetPixels().ToByteArray(PixelMapping.RGBA);
        if (pixels is null || pixels.Length == 0)
            return;

        var width = checked((int)image.Width);
        var height = checked((int)image.Height);

        foreach (var mark in normalized)
        {
            ApplyMark(pixels, width, height, mark);
        }

        image.ImportPixels(pixels, new PixelImportSettings(image.Width, image.Height, StorageType.Char, PixelMapping.RGBA));
    }

    private static void ApplyMark(byte[] pixels, int width, int height, RedEyeCorrectionMark mark)
    {
        var radius = Math.Max(MinRadius, mark.Radius);
        var radiusSquared = radius * radius;
        var left = Math.Max(0, (int)Math.Floor(mark.X - radius));
        var top = Math.Max(0, (int)Math.Floor(mark.Y - radius));
        var right = Math.Min(width - 1, (int)Math.Ceiling(mark.X + radius));
        var bottom = Math.Min(height - 1, (int)Math.Ceiling(mark.Y + radius));

        for (var y = top; y <= bottom; y++)
        {
            var dy = y - mark.Y;
            for (var x = left; x <= right; x++)
            {
                var dx = x - mark.X;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared)
                    continue;

                var index = ((y * width) + x) * 4;
                if (!IsRedEyePixel(pixels[index], pixels[index + 1], pixels[index + 2], mark.Threshold))
                    continue;

                var distance = Math.Sqrt(distanceSquared);
                var falloff = 0.5 + 0.5 * Math.Cos(Math.PI * distance / radius);
                var amount = Math.Clamp(mark.Strength * falloff, 0, 1);
                ReduceRedEye(pixels, index, amount);
            }
        }
    }

    private static bool IsRedEyePixel(byte red, byte green, byte blue, double threshold)
    {
        var dominantNeighbor = Math.Max(green, blue);
        var requiredDelta = 18 + (threshold * 86);
        return red >= 80 &&
            red - dominantNeighbor >= requiredDelta &&
            red >= green * (1.12 + threshold * 0.55) &&
            red >= blue * (1.12 + threshold * 0.55);
    }

    private static void ReduceRedEye(byte[] pixels, int index, double amount)
    {
        var red = pixels[index];
        var green = pixels[index + 1];
        var blue = pixels[index + 2];
        var neutral = (green + blue) / 2.0;
        var targetRed = Math.Min(red, neutral * 0.88);
        var targetGreen = green * 0.96;
        var targetBlue = blue * 0.96;

        pixels[index] = Blend(red, targetRed, amount);
        pixels[index + 1] = Blend(green, targetGreen, amount * 0.2);
        pixels[index + 2] = Blend(blue, targetBlue, amount * 0.2);
    }

    private static byte Blend(byte current, double target, double amount)
    {
        var blended = current + (target - current) * Math.Clamp(amount, 0, 1);
        return (byte)Math.Clamp((int)Math.Round(blended, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static string Plural(int count) => count == 1 ? "" : "s";
}
