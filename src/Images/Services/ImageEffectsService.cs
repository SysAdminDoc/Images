using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using Images.Localization;

namespace Images.Services;

public sealed record ImageEffectsPlan(
    double Sharpen,
    double NoiseReduction,
    double Vignette)
{
    public static ImageEffectsPlan Default { get; } = new(0, 0, 0);

    public bool IsIdentity =>
        Near(Sharpen, 0) &&
        Near(NoiseReduction, 0) &&
        Near(Vignette, 0);

    public string Label => IsIdentity
        ? Strings.EffectsLabel
        : Strings.Format("EffectsLabelWithSummaryFormat", Summary);

    public string Summary
    {
        get
        {
            var parts = new List<string>(3);
            if (!Near(Sharpen, 0)) parts.Add(Strings.Format("EffectsSummarySharpenFormat", Sharpen));
            if (!Near(NoiseReduction, 0)) parts.Add(Strings.Format("EffectsSummaryNoiseFormat", NoiseReduction));
            if (!Near(Vignette, 0)) parts.Add(Strings.Format("EffectsSummaryVignetteFormat", Vignette));
            return parts.Count == 0 ? Strings.EffectsNoChangesSummary : string.Join(Strings.EffectsSummarySeparator, parts);
        }
    }

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sharpen"] = Sharpen.ToString("0.###", CultureInfo.InvariantCulture),
            ["noiseReduction"] = NoiseReduction.ToString("0.###", CultureInfo.InvariantCulture),
            ["vignette"] = Vignette.ToString("0.###", CultureInfo.InvariantCulture)
        };

    private static bool Near(double left, double right)
        => Math.Abs(left - right) < 0.001;
}

public static class ImageEffectsService
{
    public static ImageEffectsPlan Normalize(ImageEffectsPlan plan)
        => new(
            ClampPercent(plan.Sharpen),
            ClampPercent(plan.NoiseReduction),
            ClampPercent(plan.Vignette));

    public static ImageEffectsPlan FromParameters(IReadOnlyDictionary<string, string> parameters)
        => Normalize(new ImageEffectsPlan(
            ParseDouble(parameters, "sharpen", 0),
            ParseDouble(parameters, "noiseReduction", 0),
            ParseDouble(parameters, "vignette", 0)));

    public static void Apply(MagickImage image, ImageEffectsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = Normalize(plan);

        if (normalized.NoiseReduction > 0)
        {
            var order = (uint)Math.Clamp((int)Math.Round(1 + normalized.NoiseReduction / 25.0), 1, 5);
            image.ReduceNoise(order);
        }

        if (normalized.Sharpen > 0)
        {
            var sigma = 0.35 + normalized.Sharpen / 100.0 * 1.65;
            var amount = 0.6 + normalized.Sharpen / 100.0 * 1.1;
            image.UnsharpMask(0, sigma, amount, 0.01);
        }

        if (normalized.Vignette > 0)
        {
            var shortest = Math.Max(1, Math.Min((int)image.Width, (int)image.Height));
            var sigma = Math.Max(1, shortest * (0.015 + normalized.Vignette / 100.0 * 0.09));
            image.BackgroundColor = MagickColors.Black;
            image.Vignette(0, sigma, 0, 0);
        }
    }

    public static BitmapSource CreatePreview(string imagePath, ImageEffectsPlan plan, int maxEdge = 820)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException(Strings.EffectsImagePathRequired, nameof(imagePath));

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

    private static double ClampPercent(double value)
        => Math.Clamp(double.IsFinite(value) ? value : 0, 0, 100);
}
