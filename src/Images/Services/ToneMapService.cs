using System.Globalization;
using ImageMagick;

namespace Images.Services;

public enum ToneMapOperator
{
    Reinhard,
    Hable,
    Aces
}

/// <summary>
/// Maps scene-linear/high-bit-depth pixels into the SDR range before BGRA8 quantization.
/// The Magick.NET HDRI build is required so samples above <see cref="Quantum.Max"/> survive decode.
/// </summary>
public static class ToneMapService
{
    private static readonly HashSet<string> CandidateExtensions = new(
        SupportedImageFormats.RawExtensions.Concat(
        [".exr", ".hdr", ".pfm", ".avif", ".heic", ".heif", ".hif", ".jxl", ".tif", ".tiff"]),
        StringComparer.OrdinalIgnoreCase);

    public static bool IsCandidateExtension(string extension)
        => CandidateExtensions.Contains(NormalizeExtension(extension));

    public static ToneMapOperator ParseOperator(string? value)
        => Enum.TryParse<ToneMapOperator>(value, ignoreCase: true, out var parsed)
            ? parsed
            : ToneMapOperator.Reinhard;

    public static bool ShouldProbe(ReadOnlySpan<byte> bytes, string extension)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            // PNG IHDR byte 8 is the sample bit depth. Animation probing has already run before
            // this check, so an ordinary 8-bit PNG keeps the WIC fast path without a second decode.
            return bytes.Length > 24 &&
                   bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                   bytes[24] > 8;
        }

        return CandidateExtensions.Contains(normalized);
    }

    public static bool IsHdrClass(IMagickImage<float> image, string extension)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = NormalizeExtension(extension);
        return image.Depth > 8 ||
               normalized is ".exr" or ".hdr" or ".pfm" ||
               SupportedImageFormats.RawExtensions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static bool ApplyIfNeeded(
        IMagickImage<float> image,
        string extension,
        ToneMapOperator toneMapOperator = ToneMapOperator.Reinhard)
    {
        if (!IsHdrClass(image, extension))
            return false;

        var statistics = image.Statistics(Channels.RGB).Composite();
        var whitePoint = Math.Max(1d, statistics.Maximum / Quantum.Max);
        image.Fx(BuildExpression(toneMapOperator, whitePoint), Channels.RGB);
        image.Depth = 8;
        image.ColorSpace = ColorSpace.sRGB;
        return true;
    }

    internal static void Apply(
        Span<float> samples,
        ToneMapOperator toneMapOperator = ToneMapOperator.Reinhard,
        float? whitePoint = null)
    {
        if (samples.IsEmpty)
            return;

        var white = Math.Max(1f, whitePoint ?? MaxFinite(samples));
        for (var i = 0; i < samples.Length; i++)
            samples[i] = MapSample(samples[i], toneMapOperator, white);
    }

    internal static float MapSample(float sample, ToneMapOperator toneMapOperator, float whitePoint)
    {
        if (!float.IsFinite(sample) || sample <= 0)
            return 0;

        var white = Math.Max(1f, whitePoint);
        var mapped = toneMapOperator switch
        {
            ToneMapOperator.Hable => Hable(sample) / Hable(white),
            ToneMapOperator.Aces => Aces(sample) / Aces(white),
            _ => sample * (1f + sample / (white * white)) / (1f + sample)
        };
        return Math.Clamp(mapped, 0f, 1f);
    }

    private static string BuildExpression(ToneMapOperator toneMapOperator, double whitePoint)
    {
        var white = whitePoint.ToString("R", CultureInfo.InvariantCulture);
        return toneMapOperator switch
        {
            ToneMapOperator.Hable =>
                $"min(1,max(0,(((u*(0.15*u+0.05)+0.004)/(u*(0.15*u+0.50)+0.06))-0.0666666667)/" +
                $"((({white}*(0.15*{white}+0.05)+0.004)/({white}*(0.15*{white}+0.50)+0.06))-0.0666666667)))",
            ToneMapOperator.Aces =>
                $"min(1,max(0,((u*(2.51*u+0.03))/(u*(2.43*u+0.59)+0.14))/" +
                $"(({white}*(2.51*{white}+0.03))/({white}*(2.43*{white}+0.59)+0.14))))",
            _ => $"min(1,max(0,u*(1+u/({white}*{white}))/(1+u)))"
        };
    }

    private static float MaxFinite(ReadOnlySpan<float> samples)
    {
        var maximum = 1f;
        foreach (var sample in samples)
        {
            if (float.IsFinite(sample))
                maximum = Math.Max(maximum, sample);
        }
        return maximum;
    }

    private static float Hable(float value)
        => ((value * (0.15f * value + 0.05f) + 0.004f) /
            (value * (0.15f * value + 0.50f) + 0.06f)) - 0.06666667f;

    private static float Aces(float value)
        => (value * (2.51f * value + 0.03f)) /
           (value * (2.43f * value + 0.59f) + 0.14f);

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;
        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
    }
}
