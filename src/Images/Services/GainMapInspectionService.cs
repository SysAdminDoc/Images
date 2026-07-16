using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Images.Services;

/// <summary>
/// Read-only detection of HDR gain maps (Google Ultra HDR, Adobe/ISO <c>hdrgm</c> metadata,
/// Apple HDR gain maps, and ISO 21496-1). Windows WIC silently ignores these, so the viewer
/// surfaces their presence, flavor, and content-boost range in the metadata panel. This is a
/// pure inspector: it never writes, decodes HDR, or fetches anything.
/// </summary>
public static partial class GainMapInspectionService
{
    // Gain-map metadata lives in ASCII XMP packets and container/aux markers near the start of a
    // file plus, for Ultra HDR, in the embedded gain-map image's XMP later in the byte stream.
    // Phone HDR stills are small; cap the scan so a pathological large file cannot be fully read.
    private const int MaxScanBytes = 32 * 1024 * 1024;

    private const string HdrgmNamespace = "ns.adobe.com/hdr-gain-map";
    private const string AppleAuxMarker = "apple:photo:2020:aux:hdrgainmap";
    private const string IsoMarker = "iso:ts:21496";

    public static GainMapInspection Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return GainMapInspection.Absent;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = (int)Math.Min(stream.Length, MaxScanBytes);
            var buffer = new byte[length];
            var read = ReadFully(stream, buffer);
            return Inspect(buffer.AsSpan(0, read));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return GainMapInspection.Absent;
        }
    }

    /// <summary>Inspects an already-loaded byte window. Exposed for testing.</summary>
    public static GainMapInspection Inspect(ReadOnlySpan<byte> bytes)
    {
        // ISO-8859-1 keeps every byte a single char so ASCII markers survive an otherwise binary
        // stream without throwing on invalid UTF-8 sequences.
        var text = Latin1.GetString(bytes);

        var appleGainMap = text.Contains(AppleAuxMarker, StringComparison.OrdinalIgnoreCase);
        var isoGainMap = text.Contains(IsoMarker, StringComparison.OrdinalIgnoreCase);
        var hasHdrgm = text.Contains(HdrgmNamespace, StringComparison.OrdinalIgnoreCase);
        var hasMultiPicture = ContainsMultiPictureFormat(bytes);

        if (!appleGainMap && !isoGainMap && !hasHdrgm)
            return GainMapInspection.Absent;

        var flavor = appleGainMap
            ? GainMapFlavor.AppleGainMap
            : isoGainMap
                ? GainMapFlavor.Iso21496
                : hasMultiPicture
                    ? GainMapFlavor.UltraHdr
                    : GainMapFlavor.GainMapMetadata;

        var version = ReadFirst(text, VersionPattern());
        var minBoost = ReadExtreme(text, GainMapMinPattern(), max: false);
        var maxBoost = ReadExtreme(text, GainMapMaxPattern(), max: true);
        var capacityMin = ReadExtreme(text, HdrCapacityMinPattern(), max: false);
        var capacityMax = ReadExtreme(text, HdrCapacityMaxPattern(), max: true);

        return new GainMapInspection(
            Present: true,
            Flavor: flavor,
            MinBoostStops: minBoost,
            MaxBoostStops: maxBoost,
            HdrCapacityMinStops: capacityMin,
            HdrCapacityMaxStops: capacityMax,
            Version: version,
            HasMultiPictureImage: hasMultiPicture);
    }

    private static bool ContainsMultiPictureFormat(ReadOnlySpan<byte> bytes)
    {
        // JPEG APP2 Multi-Picture Format segments start with the ASCII tag "MPF\0". Ultra HDR
        // stores its gain-map image as the second picture in that directory.
        ReadOnlySpan<byte> marker = [0x4D, 0x50, 0x46, 0x00];
        return bytes.IndexOf(marker) >= 0;
    }

    private static string MatchedValue(Match match)
        => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

    private static string? ReadFirst(string text, Regex pattern)
    {
        var match = pattern.Match(text);
        if (!match.Success) return null;
        var value = MatchedValue(match).Trim();
        return value.Length == 0 ? null : value;
    }

    private static double? ReadExtreme(string text, Regex pattern, bool max)
    {
        double? result = null;
        foreach (Match match in pattern.Matches(text))
        {
            foreach (var token in MatchedValue(match).Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                    double.IsNaN(value) || double.IsInfinity(value))
                {
                    continue;
                }

                result = result is null
                    ? value
                    : max ? Math.Max(result.Value, value) : Math.Min(result.Value, value);
            }
        }

        return result;
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }

        return total;
    }

    private static readonly Encoding Latin1 = Encoding.Latin1;

    // hdrgm fields appear as either XML attributes (hdrgm:GainMapMax="1.5") or elements
    // (<hdrgm:GainMapMax>1.5</hdrgm:GainMapMax>); both forms are matched.
    [GeneratedRegex(@"hdrgm:Version\s*=\s*""([^""]*)""|<hdrgm:Version>([^<]*)</hdrgm:Version>", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"hdrgm:GainMapMin\s*=\s*""([^""]*)""|<hdrgm:GainMapMin>([^<]*)</hdrgm:GainMapMin>", RegexOptions.IgnoreCase)]
    private static partial Regex GainMapMinPattern();

    [GeneratedRegex(@"hdrgm:GainMapMax\s*=\s*""([^""]*)""|<hdrgm:GainMapMax>([^<]*)</hdrgm:GainMapMax>", RegexOptions.IgnoreCase)]
    private static partial Regex GainMapMaxPattern();

    [GeneratedRegex(@"hdrgm:HDRCapacityMin\s*=\s*""([^""]*)""|<hdrgm:HDRCapacityMin>([^<]*)</hdrgm:HDRCapacityMin>", RegexOptions.IgnoreCase)]
    private static partial Regex HdrCapacityMinPattern();

    [GeneratedRegex(@"hdrgm:HDRCapacityMax\s*=\s*""([^""]*)""|<hdrgm:HDRCapacityMax>([^<]*)</hdrgm:HDRCapacityMax>", RegexOptions.IgnoreCase)]
    private static partial Regex HdrCapacityMaxPattern();
}

public enum GainMapFlavor
{
    None,
    UltraHdr,
    AppleGainMap,
    Iso21496,
    GainMapMetadata,
}

public sealed record GainMapInspection(
    bool Present,
    GainMapFlavor Flavor,
    double? MinBoostStops,
    double? MaxBoostStops,
    double? HdrCapacityMinStops,
    double? HdrCapacityMaxStops,
    string? Version,
    bool HasMultiPictureImage)
{
    public static GainMapInspection Absent { get; } =
        new(false, GainMapFlavor.None, null, null, null, null, null, false);
}
