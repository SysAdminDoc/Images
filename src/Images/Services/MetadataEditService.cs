using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
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

public sealed record MetadataStripResult(int RemovedCount, IReadOnlyList<string> RemovedTagNames)
{
    public bool ReadFailed { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// In-place metadata editing operations. Lossless formats keep their pixel
/// data bit-identical; JPEG strips re-encode at the source's estimated
/// quality (ImageMagick has no metadata-only JPEG rewrite).
/// Writes are atomic: a temp file is produced alongside the source and
/// then swapped on success, so a crash mid-write leaves the original
/// file untouched.
/// GPS strips cover EXIF GPS tags, XMP GPS/location properties, and IPTC
/// location records; "All" removes the XMP, IPTC, and Photoshop (8BIM)
/// profiles wholesale.
/// </summary>
public static class MetadataEditService
{
    private static readonly IptcTag[] IptcLocationTags =
    [
        IptcTag.City, IptcTag.SubLocation, IptcTag.ProvinceState,
        IptcTag.CountryCode, IptcTag.Country
    ];

    private static readonly HashSet<string> XmpLocationLocalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "City", "State", "Country", "CountryCode", "CountryName",
        "Location", "LocationShown", "LocationCreated", "ProvinceState", "Sublocation"
    };

    private static readonly HashSet<string> XmpLocationNamespaces = new(StringComparer.Ordinal)
    {
        "http://ns.adobe.com/photoshop/1.0/",
        "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/",
        "http://iptc.org/std/Iptc4xmpExt/2008-02-29/"
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

        using var image = MagickSafeReader.Read(path);

        var result = StripCore(image, categories);
        if (result.RemovedCount == 0)
            return result;

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

        return result;
    }

    /// <summary>
    /// Removes metadata matching the specified categories from an in-memory image.
    /// Used by copy/export workflows so source files are not changed.
    /// </summary>
    public static MetadataStripResult StripMetadata(MagickImage image, MetadataStripCategory categories)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (categories == MetadataStripCategory.None)
            return new MetadataStripResult(0, []);

        return StripCore(image, categories);
    }

    /// <summary>
    /// Previews which metadata entries would be removed for the given categories without modifying the file.
    /// </summary>
    public static MetadataStripResult PreviewStrip(string path, MetadataStripCategory categories)
    {
        if (categories == MetadataStripCategory.None)
            return new MetadataStripResult(0, []);

        try
        {
            // StripCore mutates only the in-memory image, which is discarded.
            using var image = MagickSafeReader.Read(path);
            return StripCore(image, categories);
        }
        catch (Exception ex)
        {
            return new MetadataStripResult(0, [])
            {
                ReadFailed = true,
                ErrorMessage = ex.Message
            };
        }
    }

    private static MetadataStripResult StripCore(MagickImage image, MetadataStripCategory categories)
    {
        var removedNames = new List<string>();

        var exif = image.GetExifProfile();
        if (exif is not null)
        {
            var tagsToRemove = exif.Values
                .Where(v => ShouldRemoveTag(v.Tag.ToString(), categories))
                .Select(v => v.Tag)
                .ToList();

            if (tagsToRemove.Count > 0)
            {
                removedNames.AddRange(tagsToRemove.Select(t => t.ToString()));
                foreach (var tag in tagsToRemove)
                    exif.RemoveValue(tag);
                image.SetProfile(exif);
            }
        }

        if (categories.HasFlag(MetadataStripCategory.All))
            removedNames.AddRange(RemoveSidecarProfiles(image));
        else if (categories.HasFlag(MetadataStripCategory.Gps))
            removedNames.AddRange(StripLocationData(image));

        return new MetadataStripResult(removedNames.Count, removedNames);
    }

    /// <summary>
    /// Removes the XMP, IPTC, and Photoshop resource profiles entirely.
    /// Used by the "All metadata" category, where partial preservation
    /// would leak fields the EXIF tag lists don't cover.
    /// </summary>
    private static List<string> RemoveSidecarProfiles(MagickImage image)
    {
        var removed = new List<string>();
        foreach (var name in new[] { "xmp", "iptc", "8bim" })
        {
            if (image.HasProfile(name))
            {
                image.RemoveProfile(name);
                removed.Add($"Profile:{name}");
            }
        }

        return removed;
    }

    /// <summary>
    /// Removes GPS/location data that lives outside EXIF: XMP GPS properties
    /// (exif namespace) plus photoshop/IPTC-XMP location fields, and IPTC
    /// City/Sublocation/Province/Country records. Non-location XMP content
    /// (ratings, labels, edit provenance) is preserved.
    /// </summary>
    private static List<string> StripLocationData(MagickImage image)
    {
        var removed = new List<string>();

        var iptc = image.GetIptcProfile();
        if (iptc is not null)
        {
            var touched = false;
            foreach (var tag in IptcLocationTags)
            {
                if (iptc.RemoveValue(tag))
                {
                    removed.Add($"Iptc:{tag}");
                    touched = true;
                }
            }

            if (touched)
                image.SetProfile(iptc);
        }

        var xmp = image.GetXmpProfile();
        if (xmp is null)
            return removed;

        XDocument? doc;
        try
        {
            doc = xmp.ToXDocument();
        }
        catch (XmlException)
        {
            doc = null;
        }

        if (doc is null)
        {
            // Unparseable XMP can't be selectively scrubbed; fail closed for
            // a privacy strip and drop the whole packet.
            image.RemoveProfile("xmp");
            removed.Add("Profile:xmp");
            return removed;
        }

        var elements = doc.Descendants().Where(e => IsLocationName(e.Name)).ToList();
        var elementSet = new HashSet<XElement>(elements);
        var changed = false;
        foreach (var element in elements)
        {
            if (element.Ancestors().Any(elementSet.Contains))
                continue;
            removed.Add($"Xmp:{element.Name.LocalName}");
            element.Remove();
            changed = true;
        }

        var attributes = doc.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => !a.IsNamespaceDeclaration && IsLocationName(a.Name))
            .ToList();
        foreach (var attribute in attributes)
        {
            removed.Add($"Xmp:{attribute.Name.LocalName}");
            attribute.Remove();
            changed = true;
        }

        if (changed)
        {
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };
            using var buffer = new MemoryStream();
            using (var writer = XmlWriter.Create(buffer, settings))
                doc.Save(writer);
            image.SetProfile(new XmpProfile(buffer.ToArray()));
        }

        return removed;
    }

    private static bool IsLocationName(XName name) =>
        name.LocalName.StartsWith("GPS", StringComparison.OrdinalIgnoreCase) ||
        (XmpLocationNamespaces.Contains(name.NamespaceName) &&
         XmpLocationLocalNames.Contains(name.LocalName));

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
