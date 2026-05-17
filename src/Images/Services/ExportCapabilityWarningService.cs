using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record ExportSourceFormatFacts(
    bool HasAlpha,
    bool HasMetadata,
    bool HasColorProfile,
    int AnimationFrameCount,
    int PageCount)
{
    public bool HasAnimation => AnimationFrameCount > 1;
    public bool HasPages => PageCount > 1;
}

public sealed record ExportTargetFormatCapabilities(
    string Extension,
    string FormatLabel,
    bool SupportsAlpha,
    bool SupportsAnimation,
    bool SupportsMultiplePages,
    bool SupportsMetadata,
    bool SupportsColorProfile,
    bool IsLossy);

public static class ExportCapabilityWarningService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ExportCapabilityWarningService));

    private static readonly HashSet<string> AnimationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif", ".webp", ".apng", ".png", ".mng"
    };

    public static IReadOnlyList<string> BuildWarnings(
        MagickImage image,
        string? sourcePath,
        string extension)
    {
        var normalizedExtension = RenameService.NormalizeExtension(extension);
        var format = ImageExportService.TryResolveFormat(normalizedExtension) ?? MagickFormat.Png;
        return BuildWarnings(image, sourcePath, normalizedExtension, format);
    }

    internal static IReadOnlyList<string> BuildWarnings(
        MagickImage image,
        string? sourcePath,
        string extension,
        MagickFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);

        var target = InspectTarget(extension, format);
        var source = InspectSource(image, sourcePath);
        var warnings = new List<string>();

        if (source.HasAlpha && !target.SupportsAlpha)
            warnings.Add("Transparency/alpha will be flattened to white for this target format.");

        if (source.HasAnimation)
            warnings.Add($"Animation has {source.AnimationFrameCount} frames; this export writes the displayed frame only and will not preserve animation.");

        if (source.HasPages)
            warnings.Add($"Source has {source.PageCount} pages/layers; this export writes the selected page or first layer only.");

        if (source.HasMetadata)
        {
            warnings.Add(target.SupportsMetadata
                ? "EXIF/IPTC/XMP metadata may not be preserved by this export path."
                : "EXIF/IPTC/XMP metadata will likely be dropped because this target format has limited metadata support.");
        }

        if (source.HasColorProfile)
        {
            warnings.Add(target.SupportsColorProfile
                ? "Embedded ICC color profile should be verified; Images does not yet provide color-managed export proofing."
                : "Embedded ICC color profile will likely be dropped because this target format has limited color-profile support.");
        }

        if (target.IsLossy)
            warnings.Add("Quality changes are lossy; keep the original when comparing compression settings.");

        return warnings;
    }

    public static ExportTargetFormatCapabilities InspectTarget(string extension)
    {
        var normalizedExtension = RenameService.NormalizeExtension(extension);
        var format = ImageExportService.TryResolveFormat(normalizedExtension) ?? MagickFormat.Png;
        return InspectTarget(normalizedExtension, format);
    }

    private static ExportTargetFormatCapabilities InspectTarget(string extension, MagickFormat format)
        => new(
            string.IsNullOrWhiteSpace(extension) ? ".png" : extension,
            string.IsNullOrWhiteSpace(extension) ? "PNG" : extension.TrimStart('.').ToUpperInvariant(),
            SupportsAlpha(format),
            SupportsAnimation(format),
            SupportsMultiplePages(format),
            SupportsMetadata(format),
            SupportsColorProfile(format),
            IsLossy(format));

    public static ExportSourceFormatFacts InspectSource(MagickImage image, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(image);

        var imageFacts = InspectImage(image);
        if (string.IsNullOrWhiteSpace(sourcePath))
            return imageFacts;

        try
        {
            var normalizedPath = Path.GetFullPath(sourcePath);
            if (!File.Exists(normalizedPath))
                return imageFacts;

            var fileFacts = InspectSourceFile(normalizedPath);
            return new ExportSourceFormatFacts(
                imageFacts.HasAlpha || fileFacts.HasAlpha,
                imageFacts.HasMetadata || fileFacts.HasMetadata,
                imageFacts.HasColorProfile || fileFacts.HasColorProfile,
                Math.Max(imageFacts.AnimationFrameCount, fileFacts.AnimationFrameCount),
                Math.Max(imageFacts.PageCount, fileFacts.PageCount));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.LogDebug(ex, "Could not inspect export source path {Path}", sourcePath);
            return imageFacts;
        }
    }

    private static ExportSourceFormatFacts InspectSourceFile(string path)
    {
        var frameCount = CountFrames(path);
        var firstFrame = InspectFirstFrame(path);
        var (animationFrames, pages) = ClassifySequence(path, frameCount);

        return firstFrame with
        {
            AnimationFrameCount = animationFrames,
            PageCount = pages
        };
    }

    private static ExportSourceFormatFacts InspectFirstFrame(string path)
    {
        try
        {
            CodecRuntime.Configure();

            using var image = new MagickImage();
            image.Ping(new FileInfo(path), CreateSingleFrameReadSettings(path));
            return InspectImage(image);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Log.LogDebug(ex, "Could not inspect export source metadata for {Path}", path);
            return new ExportSourceFormatFacts(false, false, false, 1, 1);
        }
    }

    private static ExportSourceFormatFacts InspectImage(MagickImage image)
    {
        var hasMetadata = false;
        var hasColorProfile = false;
        try
        {
            hasMetadata = image.GetExifProfile() is not null ||
                          image.GetIptcProfile() is not null ||
                          image.GetXmpProfile() is not null;
            hasColorProfile = image.GetColorProfile() is not null;
        }
        catch (Exception ex) when (ex is MagickException or NotSupportedException)
        {
            Log.LogDebug(ex, "Could not inspect export source profiles.");
        }

        return new ExportSourceFormatFacts(image.HasAlpha, hasMetadata, hasColorProfile, 1, 1);
    }

    private static int CountFrames(string path)
    {
        try
        {
            CodecRuntime.Configure();

            var info = SupportedImageFormats.RequiresGhostscript(path)
                ? MagickImageInfo.ReadCollection(new FileInfo(path), CreateCollectionReadSettings(path))
                : MagickImageInfo.ReadCollection(new FileInfo(path));
            return Math.Max(1, info.Count());
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Log.LogDebug(ex, "Could not count export source frames for {Path}", path);
            return 1;
        }
    }

    private static (int AnimationFrameCount, int PageCount) ClassifySequence(string path, int frameCount)
    {
        if (frameCount <= 1)
            return (1, 1);

        return AnimationExtensions.Contains(Path.GetExtension(path))
            ? (frameCount, 1)
            : (1, frameCount);
    }

    private static MagickReadSettings CreateSingleFrameReadSettings(string path)
    {
        var settings = CreateCollectionReadSettings(path);
        settings.FrameIndex = 0;
        settings.FrameCount = 1;
        return settings;
    }

    private static MagickReadSettings CreateCollectionReadSettings(string path)
    {
        var settings = new MagickReadSettings
        {
            BackgroundColor = MagickColors.White
        };

        if (SupportedImageFormats.RequiresGhostscript(path))
            settings.Density = new Density(72);

        return settings;
    }

    private static bool SupportsAlpha(MagickFormat format)
        => !ImageExportService.FormatRequiresOpaqueBackground(format);

    private static bool SupportsAnimation(MagickFormat format)
        => format is MagickFormat.Gif or MagickFormat.APng or MagickFormat.WebP or MagickFormat.Mng;

    private static bool SupportsMultiplePages(MagickFormat format)
        => format is MagickFormat.Tiff or MagickFormat.Pdf or MagickFormat.Pdfa or
            MagickFormat.Psd or MagickFormat.Psb or MagickFormat.Dcx or
            MagickFormat.Jpm or MagickFormat.Miff;

    private static bool SupportsMetadata(MagickFormat format)
        => format is MagickFormat.Jpeg or MagickFormat.Png or MagickFormat.Tiff or
            MagickFormat.WebP or MagickFormat.Avif or MagickFormat.Heic or
            MagickFormat.Jxl or MagickFormat.Psd or MagickFormat.Psb or
            MagickFormat.Pdf or MagickFormat.Pdfa or MagickFormat.Jp2 or
            MagickFormat.J2k or MagickFormat.Exr;

    private static bool SupportsColorProfile(MagickFormat format)
        => format is MagickFormat.Jpeg or MagickFormat.Png or MagickFormat.Tiff or
            MagickFormat.WebP or MagickFormat.Avif or MagickFormat.Heic or
            MagickFormat.Jxl or MagickFormat.Psd or MagickFormat.Psb or
            MagickFormat.Pdf or MagickFormat.Pdfa or MagickFormat.Jp2 or
            MagickFormat.J2k or MagickFormat.Exr;

    private static bool IsLossy(MagickFormat format)
        => format is MagickFormat.Jpeg or MagickFormat.WebP or MagickFormat.Avif or MagickFormat.Heic;
}
