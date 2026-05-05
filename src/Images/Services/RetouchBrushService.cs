using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;

namespace Images.Services;

public sealed record RetouchBrushStroke(
    double SourceX,
    double SourceY,
    double TargetX,
    double TargetY,
    double Radius,
    double Strength,
    bool Heal)
{
    public double SourceLeft => SourceX - Radius;
    public double SourceTop => SourceY - Radius;
    public double TargetLeft => TargetX - Radius;
    public double TargetTop => TargetY - Radius;
    public double Diameter => Radius * 2;
    public string ModeLabel => Heal ? "Heal" : "Clone";
}

public static class RetouchBrushService
{
    public const double MinRadius = 4;
    public const double MaxRadius = 220;
    public const double MinStrength = 0.05;
    public const double MaxStrength = 1;
    private const string StrokesKey = "strokes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RetouchBrushStroke NormalizeStroke(
        PixelCoordinate source,
        PixelCoordinate target,
        double radius,
        double strength,
        bool heal,
        int imageWidth,
        int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image dimensions must be positive.");

        return new RetouchBrushStroke(
            Math.Clamp(source.X, 0, imageWidth - 1),
            Math.Clamp(source.Y, 0, imageHeight - 1),
            Math.Clamp(target.X, 0, imageWidth - 1),
            Math.Clamp(target.Y, 0, imageHeight - 1),
            Math.Clamp(double.IsFinite(radius) ? radius : 28, MinRadius, MaxRadius),
            Math.Clamp(double.IsFinite(strength) ? strength : 0.85, MinStrength, MaxStrength),
            heal);
    }

    public static IReadOnlyDictionary<string, string> ToEditParameters(IEnumerable<RetouchBrushStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var normalized = strokes
            .Where(stroke => stroke.Radius > 0 && stroke.Strength > 0)
            .Select(stroke => new RetouchBrushStroke(
                Math.Max(0, stroke.SourceX),
                Math.Max(0, stroke.SourceY),
                Math.Max(0, stroke.TargetX),
                Math.Max(0, stroke.TargetY),
                Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                Math.Clamp(stroke.Strength, MinStrength, MaxStrength),
                stroke.Heal))
            .ToList();

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [StrokesKey] = JsonSerializer.Serialize(normalized, JsonOptions)
        };
    }

    public static IReadOnlyList<RetouchBrushStroke> FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(StrokesKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<RetouchBrushStroke>>(raw, JsonOptions)
                ?.Where(stroke => stroke.Radius > 0 && stroke.Strength > 0)
                .Select(stroke => new RetouchBrushStroke(
                    Math.Max(0, stroke.SourceX),
                    Math.Max(0, stroke.SourceY),
                    Math.Max(0, stroke.TargetX),
                    Math.Max(0, stroke.TargetY),
                    Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                    Math.Clamp(stroke.Strength, MinStrength, MaxStrength),
                    stroke.Heal))
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string CreateLabel(IReadOnlyList<RetouchBrushStroke> strokes)
    {
        if (strokes.Count == 0)
            return "Retouch";

        var clone = strokes.Count(stroke => !stroke.Heal);
        var heal = strokes.Count(stroke => stroke.Heal);
        return (clone, heal) switch
        {
            (> 0, > 0) => $"Retouch ({clone} clone, {heal} heal)",
            (> 0, 0) => $"Clone stamp ({clone} stroke{Plural(clone)})",
            (0, > 0) => $"Healing brush ({heal} stroke{Plural(heal)})",
            _ => "Retouch"
        };
    }

    public static string CreateSummary(IReadOnlyList<RetouchBrushStroke> strokes)
    {
        if (strokes.Count == 0)
            return "No retouch strokes";

        var averageRadius = strokes.Average(stroke => stroke.Radius);
        var averageStrength = strokes.Average(stroke => stroke.Strength);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{strokes.Count} stroke{Plural(strokes.Count)}, radius {averageRadius:0.#} px, strength {averageStrength:P0}");
    }

    public static void Apply(MagickImage image, IEnumerable<RetouchBrushStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(strokes);

        var maxX = Math.Max(0, (int)image.Width - 1);
        var maxY = Math.Max(0, (int)image.Height - 1);
        var normalized = strokes
            .Where(stroke => stroke.Radius > 0 && stroke.Strength > 0)
            .Select(stroke => new RetouchBrushStroke(
                Math.Clamp(stroke.SourceX, 0, maxX),
                Math.Clamp(stroke.SourceY, 0, maxY),
                Math.Clamp(stroke.TargetX, 0, maxX),
                Math.Clamp(stroke.TargetY, 0, maxY),
                Math.Clamp(stroke.Radius, MinRadius, MaxRadius),
                Math.Clamp(stroke.Strength, MinStrength, MaxStrength),
                stroke.Heal))
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
            var sourcePixels = pixels.ToArray();
            ApplyStroke(sourcePixels, pixels, width, height, stroke);
        }

        image.ImportPixels(pixels, new PixelImportSettings(image.Width, image.Height, StorageType.Char, PixelMapping.RGBA));
    }

    private static void ApplyStroke(byte[] sourcePixels, byte[] targetPixels, int width, int height, RetouchBrushStroke stroke)
    {
        var radius = Math.Max(MinRadius, stroke.Radius);
        var radiusSquared = radius * radius;
        var left = Math.Max(0, (int)Math.Floor(stroke.TargetX - radius));
        var top = Math.Max(0, (int)Math.Floor(stroke.TargetY - radius));
        var right = Math.Min(width - 1, (int)Math.Ceiling(stroke.TargetX + radius));
        var bottom = Math.Min(height - 1, (int)Math.Ceiling(stroke.TargetY + radius));
        var sourceAverage = stroke.Heal ? AverageColor(sourcePixels, width, height, stroke.SourceX, stroke.SourceY, radius) : default;
        var targetAverage = stroke.Heal ? AverageColor(sourcePixels, width, height, stroke.TargetX, stroke.TargetY, radius) : default;

        for (var y = top; y <= bottom; y++)
        {
            var dy = y - stroke.TargetY;
            for (var x = left; x <= right; x++)
            {
                var dx = x - stroke.TargetX;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared)
                    continue;

                var srcX = (int)Math.Round(stroke.SourceX + dx, MidpointRounding.AwayFromZero);
                var srcY = (int)Math.Round(stroke.SourceY + dy, MidpointRounding.AwayFromZero);
                if (srcX < 0 || srcY < 0 || srcX >= width || srcY >= height)
                    continue;

                var distance = Math.Sqrt(distanceSquared);
                var falloff = 0.5 + 0.5 * Math.Cos(Math.PI * distance / radius);
                var amount = Math.Clamp(stroke.Strength * falloff, 0, 1);
                var targetIndex = ((y * width) + x) * 4;
                var sourceIndex = ((srcY * width) + srcX) * 4;
                BlendPixel(sourcePixels, targetPixels, sourceIndex, targetIndex, amount, stroke.Heal, sourceAverage, targetAverage);
            }
        }
    }

    private static void BlendPixel(
        byte[] sourcePixels,
        byte[] targetPixels,
        int sourceIndex,
        int targetIndex,
        double amount,
        bool heal,
        Rgba sourceAverage,
        Rgba targetAverage)
    {
        for (var channel = 0; channel < 3; channel++)
        {
            var source = sourcePixels[sourceIndex + channel];
            var target = targetPixels[targetIndex + channel];
            var correctedSource = heal
                ? Math.Clamp(source - sourceAverage[channel] + targetAverage[channel], 0, 255)
                : source;
            targetPixels[targetIndex + channel] = Blend(target, correctedSource, amount);
        }
    }

    private static Rgba AverageColor(byte[] pixels, int width, int height, double centerX, double centerY, double radius)
    {
        var left = Math.Max(0, (int)Math.Floor(centerX - radius));
        var top = Math.Max(0, (int)Math.Floor(centerY - radius));
        var right = Math.Min(width - 1, (int)Math.Ceiling(centerX + radius));
        var bottom = Math.Min(height - 1, (int)Math.Ceiling(centerY + radius));
        var radiusSquared = radius * radius;
        long red = 0;
        long green = 0;
        long blue = 0;
        var count = 0;

        for (var y = top; y <= bottom; y++)
        {
            var dy = y - centerY;
            for (var x = left; x <= right; x++)
            {
                var dx = x - centerX;
                if (dx * dx + dy * dy > radiusSquared)
                    continue;

                var index = ((y * width) + x) * 4;
                red += pixels[index];
                green += pixels[index + 1];
                blue += pixels[index + 2];
                count++;
            }
        }

        return count == 0
            ? default
            : new Rgba(red / (double)count, green / (double)count, blue / (double)count);
    }

    private static byte Blend(byte current, double target, double amount)
    {
        var blended = current + (target - current) * Math.Clamp(amount, 0, 1);
        return (byte)Math.Clamp((int)Math.Round(blended, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    private readonly record struct Rgba(double Red, double Green, double Blue)
    {
        public double this[int channel] => channel switch
        {
            0 => Red,
            1 => Green,
            _ => Blue
        };
    }
}
