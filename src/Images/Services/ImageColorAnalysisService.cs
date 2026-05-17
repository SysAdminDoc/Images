using System.Globalization;
using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record ImageColorAnalysis(
    IReadOnlyList<MetadataFact> Rows,
    string StatusText,
    string WarningText)
{
    public static ImageColorAnalysis Empty { get; } = new(Array.Empty<MetadataFact>(), "", "");
}

public static class ImageColorAnalysisService
{
    private const uint MaxSampleEdge = 512;
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ImageColorAnalysisService));

    public static ImageColorAnalysis Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return ImageColorAnalysis.Empty;

        if (SupportedImageFormats.IsArchive(path))
            return new ImageColorAnalysis(
                [],
                "Color analysis is unavailable for archive book entries.",
                "");

        try
        {
            CodecRuntime.Configure();

            using var image = ReadFirstFrame(path);
            var profile = image.GetColorProfile();
            var stats = BuildChannelStats(image);
            var rows = new List<MetadataFact>(8)
            {
                new("Profile", FormatProfile(profile)),
                new("Color space", image.ColorSpace.ToString()),
                new("Luma", stats.LumaText),
                new("Red", stats.RedText),
                new("Green", stats.GreenText),
                new("Blue", stats.BlueText),
                new("Histogram", stats.HistogramText)
            };

            if (stats.HasAlpha)
                rows.Add(new MetadataFact("Alpha", stats.AlphaText));

            var warning = profile is null
                ? "No embedded ICC profile was found; Images is reporting decoded pixels without applying a managed display transform."
                : "Embedded ICC profile found. Images reports profile and histogram data only; it does not change pixels or soft-proof output yet.";

            return new ImageColorAnalysis(rows, "", warning);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Log.LogDebug(ex, "Color analysis failed for {Path}", path);
            return new ImageColorAnalysis(
                [],
                "Color analysis is unavailable for this image.",
                "");
        }
    }

    private static MagickImage ReadFirstFrame(string path)
    {
        var settings = new MagickReadSettings
        {
            FrameIndex = 0,
            FrameCount = 1,
            BackgroundColor = MagickColors.White
        };

        if (SupportedImageFormats.RequiresGhostscript(path))
            settings.Density = new Density(72);

        using var stream = OpenSharedRead(path);
        using var frames = new MagickImageCollection(stream, settings);
        if (frames.Count == 0)
            throw new InvalidOperationException("No color-analysis frame was decoded.");

        return (MagickImage)frames[0].Clone();
    }

    private static FileStream OpenSharedRead(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static ChannelStats BuildChannelStats(MagickImage image)
    {
        using var sample = image.Clone();
        sample.Alpha(AlphaOption.Set);
        sample.Resize(new MagickGeometry(MaxSampleEdge, MaxSampleEdge)
        {
            Greater = true
        });

        var width = checked((int)sample.Width);
        var height = checked((int)sample.Height);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Decoded image dimensions are not supported for color analysis.");

        var pixels = sample.GetPixelsUnsafe().ToByteArray(PixelMapping.RGBA)
                     ?? throw new InvalidOperationException("Magick.NET returned null color-analysis pixels.");
        var count = checked(width * height);
        if (pixels.Length < count * 4)
            throw new InvalidOperationException("Magick.NET returned an unexpected color-analysis pixel buffer.");

        var red = new ChannelAccumulator();
        var green = new ChannelAccumulator();
        var blue = new ChannelAccumulator();
        var alpha = new ChannelAccumulator();
        var shadows = 0;
        var highlights = 0;
        var transparent = 0;
        double lumaSum = 0;

        for (var offset = 0; offset < count * 4; offset += 4)
        {
            var r = pixels[offset + 0];
            var g = pixels[offset + 1];
            var b = pixels[offset + 2];
            var a = pixels[offset + 3];

            red.Add(r);
            green.Add(g);
            blue.Add(b);
            alpha.Add(a);

            var luma = 0.299 * r + 0.587 * g + 0.114 * b;
            lumaSum += luma;
            if (luma < 64)
                shadows++;
            else if (luma >= 192)
                highlights++;

            if (a < 255)
                transparent++;
        }

        var midtones = count - shadows - highlights;
        var hasAlpha = alpha.Minimum < 255 || transparent > 0;
        return new ChannelStats(
            FormatMean("R", red),
            FormatMean("G", green),
            FormatMean("B", blue),
            $"mean {FormatPercent(lumaSum / count / 255d)}",
            $"shadows {FormatPercent(shadows / (double)count)} / mid {FormatPercent(midtones / (double)count)} / highlights {FormatPercent(highlights / (double)count)}",
            hasAlpha
                ? $"mean {FormatPercent(alpha.Mean / 255d)}, transparent {FormatPercent(transparent / (double)count)}"
                : "opaque",
            hasAlpha);
    }

    private static string FormatProfile(IColorProfile? profile)
    {
        if (profile is null)
            return "No embedded ICC profile";

        var description = Clean(profile.Description);
        var colorSpace = profile.ColorSpace.ToString();
        return description is null
            ? $"Embedded ICC ({colorSpace})"
            : $"Embedded ICC: {description} ({colorSpace})";
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = new string(value
            .Select(c => char.IsControl(c) ? ' ' : c)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static string FormatMean(string label, ChannelAccumulator channel)
        => string.Create(CultureInfo.InvariantCulture, $"{label} min {channel.Minimum}, mean {channel.Mean:0.#}, max {channel.Maximum}");

    private static string FormatPercent(double value)
        => string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(value, 0d, 1d) * 100:0.#}%");

    private sealed record ChannelStats(
        string RedText,
        string GreenText,
        string BlueText,
        string LumaText,
        string HistogramText,
        string AlphaText,
        bool HasAlpha);

    private sealed class ChannelAccumulator
    {
        private long _sum;
        private int _count;

        public int Minimum { get; private set; } = 255;
        public int Maximum { get; private set; }
        public double Mean => _count == 0 ? 0 : _sum / (double)_count;

        public void Add(byte value)
        {
            Minimum = Math.Min(Minimum, value);
            Maximum = Math.Max(Maximum, value);
            _sum += value;
            _count++;
        }
    }
}
