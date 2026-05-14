using System.Globalization;
using ImageMagick;

namespace Images.Services;

public sealed record ImageAutoEnhancePlan(string Profile, int Version)
{
    public static ImageAutoEnhancePlan Balanced { get; } = new("balanced", 1);

    public string Label => "Auto Enhance";

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["profile"] = Profile,
            ["version"] = Version.ToString(CultureInfo.InvariantCulture)
        };
}

public static class ImageAutoEnhanceService
{
    public static ImageAutoEnhancePlan FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        var profile = parameters.TryGetValue("profile", out var rawProfile) && !string.IsNullOrWhiteSpace(rawProfile)
            ? rawProfile.Trim().ToLowerInvariant()
            : ImageAutoEnhancePlan.Balanced.Profile;
        var version = parameters.TryGetValue("version", out var rawVersion) &&
                      int.TryParse(rawVersion, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(1, parsed)
            : ImageAutoEnhancePlan.Balanced.Version;

        return new ImageAutoEnhancePlan(profile, version);
    }

    public static void Apply(MagickImage image, ImageAutoEnhancePlan plan)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = FromParameters(plan.ToEditParameters());
        if (!normalized.Profile.Equals("balanced", StringComparison.OrdinalIgnoreCase))
            return;

        image.AutoGamma();
        image.WhiteBalance(new Percentage(0.5));
        image.SigmoidalContrast(2.6, new Percentage(50));
        image.UnsharpMask(0, 0.75, 0.75, 0.012);
    }
}
