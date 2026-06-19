using System.IO;
using ImageMagick;

namespace Images.Services;

[Flags]
public enum MetadataStripCategory
{
    None = 0,
    Gps = 1 << 0,
    DeviceInfo = 1 << 1,
    Timestamps = 1 << 2,
    Software = 1 << 3,
    All = Gps | DeviceInfo | Timestamps | Software
}

public sealed record MetadataStripResult(int RemovedCount, IReadOnlyList<string> RemovedTagNames);

/// <summary>
/// In-place metadata editing operations that preserve pixel data.
/// Writes are atomic: a temp file is produced alongside the source and
/// then swapped on success, so a crash mid-write leaves the original
/// file untouched.
/// </summary>
public static class MetadataEditService
{
    private static readonly HashSet<string> GpsPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gps"
    };

    private static readonly HashSet<string> DeviceInfoTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Make", "Model", "BodySerialNumber", "LensSerialNumber",
        "LensMake", "LensModel", "LensSpecification",
        "CameraOwnerName", "ImageUniqueID", "SerialNumber"
    };

    private static readonly HashSet<string> TimestampTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "DateTime", "DateTimeOriginal", "DateTimeDigitized",
        "OffsetTime", "OffsetTimeOriginal", "OffsetTimeDigitized",
        "SubsecTime", "SubsecTimeOriginal", "SubsecTimeDigitized"
    };

    private static readonly HashSet<string> SoftwareTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "Software", "ProcessingSoftware", "HostComputer",
        "ImageDescription", "UserComment"
    };

    /// <summary>
    /// Removes all GPS EXIF values from <paramref name="path"/> and
    /// writes the result back in-place.  Returns the number of GPS
    /// fields that were removed; returns 0 if no GPS data was present.
    /// </summary>
    public static int StripGpsMetadata(string path)
    {
        var result = StripMetadata(path, MetadataStripCategory.Gps);
        return result.RemovedCount;
    }

    /// <summary>
    /// Removes EXIF tags matching the specified categories from <paramref name="path"/>
    /// and writes the result back in-place. Returns details about what was removed.
    /// </summary>
    public static MetadataStripResult StripMetadata(string path, MetadataStripCategory categories)
    {
        if (categories == MetadataStripCategory.None)
            return new MetadataStripResult(0, []);

        using var image = new MagickImage(path);

        var exif = image.GetExifProfile();
        if (exif is null)
            return new MetadataStripResult(0, []);

        var tagsToRemove = exif.Values
            .Where(v => ShouldRemoveTag(v.Tag.ToString(), categories))
            .Select(v => v.Tag)
            .ToList();

        if (tagsToRemove.Count == 0)
            return new MetadataStripResult(0, []);

        var removedNames = tagsToRemove.Select(t => t.ToString()).ToList();

        foreach (var tag in tagsToRemove)
            exif.RemoveValue(tag);

        image.SetProfile(exif);

        var dir = Path.GetDirectoryName(path) ?? Path.GetTempPath();
        var tmp = Path.Combine(dir, $".images-{Guid.NewGuid():N}{Path.GetExtension(path)}.tmp");
        try
        {
            image.Write(tmp);
            File.Replace(tmp, path, null);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }

        return new MetadataStripResult(tagsToRemove.Count, removedNames);
    }

    /// <summary>
    /// Removes EXIF tags matching the specified categories from an in-memory image.
    /// Used by copy/export workflows so source files are not changed.
    /// </summary>
    public static MetadataStripResult StripMetadata(MagickImage image, MetadataStripCategory categories)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (categories == MetadataStripCategory.None)
            return new MetadataStripResult(0, []);

        var exif = image.GetExifProfile();
        if (exif is null)
            return new MetadataStripResult(0, []);

        var tagsToRemove = exif.Values
            .Where(v => ShouldRemoveTag(v.Tag.ToString(), categories))
            .Select(v => v.Tag)
            .ToList();

        if (tagsToRemove.Count == 0)
            return new MetadataStripResult(0, []);

        var removedNames = tagsToRemove.Select(t => t.ToString()).ToList();

        foreach (var tag in tagsToRemove)
            exif.RemoveValue(tag);

        image.SetProfile(exif);
        return new MetadataStripResult(tagsToRemove.Count, removedNames);
    }

    /// <summary>
    /// Previews which EXIF tags would be removed for the given categories without modifying the file.
    /// </summary>
    public static MetadataStripResult PreviewStrip(string path, MetadataStripCategory categories)
    {
        if (categories == MetadataStripCategory.None)
            return new MetadataStripResult(0, []);

        try
        {
            using var image = new MagickImage(path);
            var exif = image.GetExifProfile();
            if (exif is null)
                return new MetadataStripResult(0, []);

            var matching = exif.Values
                .Where(v => ShouldRemoveTag(v.Tag.ToString(), categories))
                .Select(v => v.Tag.ToString())
                .ToList();

            return new MetadataStripResult(matching.Count, matching);
        }
        catch
        {
            return new MetadataStripResult(0, []);
        }
    }

    private static bool ShouldRemoveTag(string tagName, MetadataStripCategory categories)
    {
        if (categories.HasFlag(MetadataStripCategory.Gps) &&
            tagName.StartsWith("Gps", StringComparison.OrdinalIgnoreCase))
            return true;

        if (categories.HasFlag(MetadataStripCategory.DeviceInfo) &&
            DeviceInfoTags.Contains(tagName))
            return true;

        if (categories.HasFlag(MetadataStripCategory.Timestamps) &&
            TimestampTags.Contains(tagName))
            return true;

        if (categories.HasFlag(MetadataStripCategory.Software) &&
            SoftwareTags.Contains(tagName))
            return true;

        return false;
    }

    /// <summary>
    /// Returns the human-readable label for a metadata strip category.
    /// </summary>
    public static string CategoryLabel(MetadataStripCategory category) => category switch
    {
        MetadataStripCategory.Gps => "GPS location",
        MetadataStripCategory.DeviceInfo => "Device info (make, model, serial)",
        MetadataStripCategory.Timestamps => "Timestamps",
        MetadataStripCategory.Software => "Software and comments",
        MetadataStripCategory.All => "All metadata",
        _ => category.ToString()
    };
}
