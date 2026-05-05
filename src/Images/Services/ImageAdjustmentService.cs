using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

public sealed record ImageAdjustmentPlan(
    double BlackPoint,
    double WhitePoint,
    double Gamma,
    double Curve,
    double Hue,
    double Saturation,
    double Lightness)
{
    public static ImageAdjustmentPlan Default { get; } = new(0, 100, 1, 0, 0, 100, 100);

    public bool IsIdentity =>
        Near(BlackPoint, 0) &&
        Near(WhitePoint, 100) &&
        Near(Gamma, 1) &&
        Near(Curve, 0) &&
        Near(Hue, 0) &&
        Near(Saturation, 100) &&
        Near(Lightness, 100);

    public string Label => IsIdentity
        ? "Adjust"
        : $"Adjust levels/HSL ({Summary})";

    public string Summary
    {
        get
        {
            var parts = new List<string>(7);
            if (!Near(BlackPoint, 0)) parts.Add($"black {BlackPoint:0.#}%");
            if (!Near(WhitePoint, 100)) parts.Add($"white {WhitePoint:0.#}%");
            if (!Near(Gamma, 1)) parts.Add($"gamma {Gamma:0.##}");
            if (!Near(Curve, 0)) parts.Add($"curve {Curve:+0.#;-0.#;0}");
            if (!Near(Hue, 0)) parts.Add($"hue {Hue:+0.#;-0.#;0} deg");
            if (!Near(Saturation, 100)) parts.Add($"sat {Saturation:0.#}%");
            if (!Near(Lightness, 100)) parts.Add($"light {Lightness:0.#}%");
            return parts.Count == 0 ? "no changes" : string.Join(", ", parts);
        }
    }

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["blackPoint"] = BlackPoint.ToString("0.###", CultureInfo.InvariantCulture),
            ["whitePoint"] = WhitePoint.ToString("0.###", CultureInfo.InvariantCulture),
            ["gamma"] = Gamma.ToString("0.###", CultureInfo.InvariantCulture),
            ["curve"] = Curve.ToString("0.###", CultureInfo.InvariantCulture),
            ["hue"] = Hue.ToString("0.###", CultureInfo.InvariantCulture),
            ["saturation"] = Saturation.ToString("0.###", CultureInfo.InvariantCulture),
            ["lightness"] = Lightness.ToString("0.###", CultureInfo.InvariantCulture)
        };

    private static bool Near(double left, double right)
        => Math.Abs(left - right) < 0.001;
}

public static class ImageAdjustmentService
{
    public static ImageAdjustmentPlan Normalize(ImageAdjustmentPlan plan)
    {
        var black = Math.Clamp(plan.BlackPoint, 0, 99);
        var white = Math.Clamp(plan.WhitePoint, 1, 100);
        if (white <= black)
            white = Math.Min(100, black + 1);

        return new ImageAdjustmentPlan(
            black,
            white,
            Math.Clamp(plan.Gamma, 0.1, 5),
            Math.Clamp(plan.Curve, -50, 50),
            Math.Clamp(plan.Hue, -180, 180),
            Math.Clamp(plan.Saturation, 0, 200),
            Math.Clamp(plan.Lightness, 0, 200));
    }

    public static ImageAdjustmentPlan FromParameters(IReadOnlyDictionary<string, string> parameters)
        => Normalize(new ImageAdjustmentPlan(
            ParseDouble(parameters, "blackPoint", 0),
            ParseDouble(parameters, "whitePoint", 100),
            ParseDouble(parameters, "gamma", 1),
            ParseDouble(parameters, "curve", 0),
            ParseDouble(parameters, "hue", 0),
            ParseDouble(parameters, "saturation", 100),
            ParseDouble(parameters, "lightness", 100)));

    public static void Apply(MagickImage image, ImageAdjustmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = Normalize(plan);

        if (!CloseTo(normalized.BlackPoint, 0) || !CloseTo(normalized.WhitePoint, 100) || !CloseTo(normalized.Gamma, 1))
        {
            image.Level(
                new Percentage(normalized.BlackPoint),
                new Percentage(normalized.WhitePoint),
                normalized.Gamma);
        }

        if (!CloseTo(normalized.Curve, 0))
        {
            var contrast = Math.Abs(normalized.Curve) / 5.0;
            var midpoint = new Percentage(50);
            if (normalized.Curve > 0)
                image.SigmoidalContrast(contrast, midpoint);
            else
                image.InverseSigmoidalContrast(contrast, midpoint);
        }

        if (!CloseTo(normalized.Hue, 0) || !CloseTo(normalized.Saturation, 100) || !CloseTo(normalized.Lightness, 100))
        {
            var huePercent = Math.Clamp(100 + normalized.Hue / 180.0 * 100.0, 0, 200);
            image.Modulate(
                new Percentage(normalized.Lightness),
                new Percentage(normalized.Saturation),
                new Percentage(huePercent));
        }
    }

    public static BitmapSource CreatePreview(string imagePath, ImageAdjustmentPlan plan, int maxEdge = 820)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("An image path is required.", nameof(imagePath));

        using var image = new MagickImage(imagePath);
        image.AutoOrient();
        if (maxEdge > 0)
        {
            image.Resize(new MagickGeometry((uint)maxEdge, (uint)maxEdge)
            {
                Greater = true
            });
        }

        Apply(image, plan);
        return ToBitmapSource(image);
    }

    private static BitmapSource ToBitmapSource(MagickImage image)
    {
        var bytes = image.ToByteArray(MagickFormat.Png);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> parameters, string key, double fallback)
        => parameters.TryGetValue(key, out var value) &&
           double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool CloseTo(double left, double right)
        => Math.Abs(left - right) < 0.001;
}
