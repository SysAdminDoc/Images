using System.Globalization;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// App-enforced Magick.NET guardrail for untrusted local files. Magick.NET does not ship an
/// Images-specific policy.xml, so the viewer pins resource limits and blocks high-risk write
/// delegates before any decode/export/report path uses ImageMagick.
/// </summary>
public static class MagickSecurityPolicy
{
    public const ulong MemoryLimitBytes = 2UL * 1024 * 1024 * 1024;
    public const ulong DiskLimitBytes = 4UL * 1024 * 1024 * 1024;
    public const ulong MaxMemoryRequestBytes = 512UL * 1024 * 1024;
    public const ulong MaxProfileSizeBytes = 64UL * 1024 * 1024;
    public const int DimensionLimitPixels = 32768;
    public const ulong AreaLimitPixels = 1_000_000_000;
    public const int RenderDimensionLimitPixels = 30000;
    public const int ListLengthLimit = 128;
    public const int TimeLimitSeconds = 120;
    public const int ThreadLimit = 1;

    public static readonly string[] DisallowedWriteExtensions =
    [
        ".pdf", ".pdfa", ".ps", ".ps2", ".ps3", ".eps", ".epsf", ".epsi",
        ".ai", ".svg", ".svgz", ".mvg", ".msvg", ".msl", ".http", ".https", ".url"
    ];

    private static readonly HashSet<string> DisallowedWriteExtensionSet = new(
        DisallowedWriteExtensions,
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DisallowedWriteFormatSet = new(
        DisallowedWriteExtensions.Select(extension => extension.TrimStart('.')),
        StringComparer.OrdinalIgnoreCase);

    public static MagickSecurityPolicyReport Configure(
        bool documentDelegateAvailable,
        string documentDelegateSource)
    {
        ResourceLimits.Memory = MemoryLimitBytes;
        ResourceLimits.Disk = DiskLimitBytes;
        ResourceLimits.MaxMemoryRequest = MaxMemoryRequestBytes;
        ResourceLimits.MaxProfileSize = MaxProfileSizeBytes;
        ResourceLimits.Width = DimensionLimitPixels;
        ResourceLimits.Height = DimensionLimitPixels;
        ResourceLimits.Area = AreaLimitPixels;
        ResourceLimits.ListLength = ListLengthLimit;
        ResourceLimits.Time = TimeLimitSeconds;
        ResourceLimits.Thread = ThreadLimit;

        return Inspect(documentDelegateAvailable, documentDelegateSource);
    }

    public static MagickSecurityPolicyReport Inspect(
        bool documentDelegateAvailable,
        string documentDelegateSource)
    {
        var memory = Convert.ToUInt64(ResourceLimits.Memory, CultureInfo.InvariantCulture);
        var disk = Convert.ToUInt64(ResourceLimits.Disk, CultureInfo.InvariantCulture);
        var maxRequest = Convert.ToUInt64(ResourceLimits.MaxMemoryRequest, CultureInfo.InvariantCulture);
        var maxProfile = Convert.ToUInt64(ResourceLimits.MaxProfileSize, CultureInfo.InvariantCulture);
        var width = Convert.ToUInt64(ResourceLimits.Width, CultureInfo.InvariantCulture);
        var height = Convert.ToUInt64(ResourceLimits.Height, CultureInfo.InvariantCulture);
        var area = Convert.ToUInt64(ResourceLimits.Area, CultureInfo.InvariantCulture);
        var listLength = Convert.ToUInt64(ResourceLimits.ListLength, CultureInfo.InvariantCulture);
        var time = Convert.ToUInt64(ResourceLimits.Time, CultureInfo.InvariantCulture);
        var threads = Convert.ToUInt64(ResourceLimits.Thread, CultureInfo.InvariantCulture);

        var limitsMatch =
            memory == MemoryLimitBytes &&
            disk == DiskLimitBytes &&
            maxRequest == MaxMemoryRequestBytes &&
            maxProfile == MaxProfileSizeBytes &&
            width == DimensionLimitPixels &&
            height == DimensionLimitPixels &&
            area == AreaLimitPixels &&
            listLength == ListLengthLimit &&
            time == TimeLimitSeconds &&
            threads == ThreadLimit;

        var delegateStatus = documentDelegateAvailable
            ? $"PDF/EPS/PS/AI previews route through Ghostscript ({documentDelegateSource}); high-risk document/vector writes stay blocked."
            : "PDF/EPS/PS/AI previews are disabled until Ghostscript is configured; high-risk document/vector writes stay blocked.";

        var summary = limitsMatch
            ? "enforced resource limits, Ghostscript-gated document previews, and blocked high-risk write targets"
            : "resource limit drift detected; document preview and write-target gates still reported";

        return new MagickSecurityPolicyReport(
            IsEnforced: limitsMatch,
            Summary: summary,
            MemoryLimitBytes: memory,
            DiskLimitBytes: disk,
            MaxMemoryRequestBytes: maxRequest,
            MaxProfileSizeBytes: maxProfile,
            WidthLimitPixels: width,
            HeightLimitPixels: height,
            AreaLimitPixels: area,
            RenderDimensionLimitPixels: RenderDimensionLimitPixels,
            ListLengthLimit: listLength,
            TimeLimitSeconds: time,
            ThreadLimit: threads,
            DisallowedWriteExtensions: DisallowedWriteExtensions,
            DocumentDelegateStatus: delegateStatus);
    }

    public static bool IsRenderableDimensions(long width, long height)
    {
        if (width <= 0 || height <= 0) return false;
        if (width > RenderDimensionLimitPixels || height > RenderDimensionLimitPixels) return false;

        var pixels = checked((ulong)width * (ulong)height);
        return pixels <= AreaLimitPixels;
    }

    public static bool IsWriteTargetAllowed(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return !string.IsNullOrEmpty(normalized) &&
               !DisallowedWriteExtensionSet.Contains(normalized);
    }

    public static bool IsWriteFormatAllowed(MagickFormat format)
        => !DisallowedWriteFormatSet.Contains(format.ToString());

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        var trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal)
            ? trimmed.ToLowerInvariant()
            : "." + trimmed.ToLowerInvariant();
    }
}

public sealed record MagickSecurityPolicyReport(
    bool IsEnforced,
    string Summary,
    ulong MemoryLimitBytes,
    ulong DiskLimitBytes,
    ulong MaxMemoryRequestBytes,
    ulong MaxProfileSizeBytes,
    ulong WidthLimitPixels,
    ulong HeightLimitPixels,
    ulong AreaLimitPixels,
    int RenderDimensionLimitPixels,
    ulong ListLengthLimit,
    ulong TimeLimitSeconds,
    ulong ThreadLimit,
    IReadOnlyList<string> DisallowedWriteExtensions,
    string DocumentDelegateStatus)
{
    public string EnforcementText => IsEnforced ? "enforced" : "drift detected";

    public string ResourceLimitSummary =>
        $"memory {FormatBytes(MemoryLimitBytes)}, disk {FormatBytes(DiskLimitBytes)}, " +
        $"max request {FormatBytes(MaxMemoryRequestBytes)}, profile {FormatBytes(MaxProfileSizeBytes)}, " +
        $"dimensions {WidthLimitPixels.ToString(CultureInfo.InvariantCulture)} x {HeightLimitPixels.ToString(CultureInfo.InvariantCulture)}, " +
        $"render {RenderDimensionLimitPixels.ToString(CultureInfo.InvariantCulture)} px/edge, " +
        $"area {AreaLimitPixels.ToString("N0", CultureInfo.InvariantCulture)} px, " +
        $"list {ListLengthLimit.ToString(CultureInfo.InvariantCulture)}, " +
        $"time {TimeLimitSeconds.ToString(CultureInfo.InvariantCulture)}s, " +
        $"threads {ThreadLimit.ToString(CultureInfo.InvariantCulture)}";

    public string BlockedWriteSummary => string.Join(", ", DisallowedWriteExtensions);

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var unit = 0;
        double value = bytes;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes.ToString(CultureInfo.InvariantCulture)} B"
            : $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
