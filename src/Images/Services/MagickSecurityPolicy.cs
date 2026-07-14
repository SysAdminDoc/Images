using System.Globalization;
using System.IO;
using ImageMagick.Configuration;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// App-enforced Magick.NET guardrail for untrusted local files. Magick.NET does not ship an
/// Images-specific policy.xml, so the viewer injects one before native initialization, pins
/// resource limits, and blocks unneeded coders/delegates before any decode/export/report path
/// uses ImageMagick.
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
    // Keep this list explicit and reviewable. It is intentionally narrower than every coder
    // compiled into ImageMagick: direct pseudo-coders (MSL/LABEL/URL/etc.) and historically risky
    // exotic decoders such as MNG and TIM are not reachable from untrusted files.
    public static readonly string[] AllowedReadWriteCoders =
    [
        "APNG", "AVIF", "BMP", "CIN", "CUR", "DCX", "DDS", "DIB", "DPX", "EXR",
        "FARBFELD", "FAX", "FITS", "G3", "G4", "GIF", "HDR", "HEIC", "HEIF", "HIF",
        "ICO", "J2C", "J2K", "JFIF", "JNG", "JP2", "JPC", "JPE", "JPEG", "JPG", "JPF",
        "JPM", "JPS", "JPT", "JPX", "JXL", "MIFF", "OTB", "PAM", "PBM", "PCD",
        "PCDS", "PCT", "PCX", "PFM", "PGM", "PGX", "PIC", "PICT", "PICON", "PIX",
        "PNG", "PNM", "PPM", "PSB", "PSD", "QOI", "RAS", "RGB", "RGBA", "RLE",
        "SGI", "SIX", "SIXEL", "SUN", "TARGA", "TGA", "TIF", "TIFF", "VICAR",
        "VIFF", "VIPS", "WBMP", "WEBP", "XBM", "XC", "XCF", "XPM", "XWD"
    ];

    public static readonly string[] AllowedReadOnlyCoders =
    [
        // Vector inputs are rasterized for display but may not be emitted by ImageMagick.
        // MSVG emits an in-memory MVG drawing stream. Neither extension is exposed as a user
        // input, but both native coders are required for delegate-free SVG rasterization.
        "EMF", "MSVG", "MVG", "SVG", "SVGZ", "WMF", "WPG",

        // Camera formats remain readable when the bundled native build has an in-process coder.
        // Any build that requires an external delegate fails closed because delegates are denied.
        "3FR", "ARW", "BAY", "CAP", "CR2", "CR3", "CRW", "DCR", "DNG", "ERF",
        "FFF", "GPR", "IIQ", "K25", "KDC", "MEF", "MOS", "MRW", "NEF", "NRW",
        "ORF", "PEF", "RAF", "RAW", "RW2", "RWL", "SR2", "SRF", "SRW", "X3F",

        // Declared read-only raster formats without a supported export path.
        "DCM", "DICOM", "ORA", "PWP", "SFW", "XV"
    ];

    public static readonly string[] DocumentReadOnlyCoders =
    [
        "AI", "EPDF", "EPS", "EPS2", "EPS3", "EPSF", "EPSI", "EPT", "EPT2", "EPT3",
        "PDF", "PDFA", "PS", "PS2", "PS3"
    ];

    public static readonly string[] AllowedDocumentDelegates =
    [
        "ps:alpha", "ps:cmyk", "ps:color", "ps:mono"
    ];

    private static readonly HashSet<string> AllowedReadCoderSet = new(
        AllowedReadWriteCoders.Concat(AllowedReadOnlyCoders).Concat(DocumentReadOnlyCoders),
        StringComparer.OrdinalIgnoreCase);

    private static readonly object NativePolicySync = new();
    private static bool _nativePolicyInitialized;
    private static bool _documentDelegatesEnabled;
    private static string? _temporaryConfigurationPath;

    public static bool DocumentDelegatesEnabled => _documentDelegatesEnabled;

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
        EnsureNativePolicyInitialized(documentDelegateAvailable);

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

        var delegateStatus = _documentDelegatesEnabled
            ? $"All delegates are denied except four Ghostscript raster-preview delegates ({documentDelegateSource}); high-risk document/vector writes stay blocked."
            : "All external ImageMagick delegates are disabled; PDF/EPS/PS/AI previews and high-risk document/vector writes stay blocked.";

        var policyInitialized = _nativePolicyInitialized;
        var summary = limitsMatch && policyInitialized
            ? "enforced resource limits, a native read-coder allowlist, default-denied delegates, disabled path indirection, and blocked high-risk write targets"
            : "Magick.NET security-policy drift detected";

        return new MagickSecurityPolicyReport(
            IsEnforced: limitsMatch && policyInitialized,
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
            AllowedReadCoders: AllowedReadCoderSet.OrderBy(static coder => coder, StringComparer.Ordinal).ToArray(),
            DelegatesDefaultDenied: policyInitialized,
            DocumentDelegatesEnabled: _documentDelegatesEnabled,
            DisallowedWriteExtensions: DisallowedWriteExtensions,
            DocumentDelegateStatus: delegateStatus);
    }

    public static bool IsReadCoderAllowed(string coder)
        => !string.IsNullOrWhiteSpace(coder) && AllowedReadCoderSet.Contains(coder.Trim());

    internal static string CreateNativePolicyXml(bool allowDocumentDelegates = false)
    {
        var readWritePattern = "{" + string.Join(',', AllowedReadWriteCoders) + "}";
        var readOnlyPattern = "{" + string.Join(',', AllowedReadOnlyCoders.Concat(DocumentReadOnlyCoders)) + "}";
        var documentDelegatePolicy = allowDocumentDelegates
            ? $"  <policy domain=\"delegate\" rights=\"execute\" pattern=\"{{{string.Join(',', AllowedDocumentDelegates)}}}\" />"
            : string.Empty;

        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <policymap>
              <policy domain="delegate" rights="none" pattern="*" />
            {{documentDelegatePolicy}}
              <policy domain="filter" rights="none" pattern="*" />
              <policy domain="path" rights="none" pattern="@*" />
              <policy domain="coder" rights="none" pattern="*" />
              <policy domain="coder" rights="read | write" pattern="{{readWritePattern}}" />
              <policy domain="coder" rights="read" pattern="{{readOnlyPattern}}" />
            </policymap>
            """;
    }

    private static void EnsureNativePolicyInitialized(bool allowDocumentDelegates)
    {
        lock (NativePolicySync)
        {
            if (_nativePolicyInitialized) return;

            var configuration = ConfigurationFiles.Default;
            configuration.Policy.Data = CreateNativePolicyXml(allowDocumentDelegates);
            _temporaryConfigurationPath = MagickNET.Initialize(configuration);

            AppDomain.CurrentDomain.ProcessExit += static (_, _) => DeleteTemporaryConfiguration();
            _documentDelegatesEnabled = allowDocumentDelegates;
            _nativePolicyInitialized = true;
        }
    }

    private static void DeleteTemporaryConfiguration()
    {
        var path = _temporaryConfigurationPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The OS temp directory remains the fallback cleanup owner after an abnormal exit or
            // if ImageMagick still has a configuration file open during process teardown.
        }
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
    IReadOnlyList<string> AllowedReadCoders,
    bool DelegatesDefaultDenied,
    bool DocumentDelegatesEnabled,
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

    public string ReadPolicySummary =>
        $"{AllowedReadCoders.Count.ToString(CultureInfo.InvariantCulture)} coders allowed; " +
        (DocumentDelegatesEnabled
            ? "delegates default-denied with Ghostscript raster previews allowed; filters and @ paths disabled"
            : "delegates, filters, and @ paths disabled");

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
