using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// Reads XMP sidecar files and extracts DAM metadata: ratings, color labels, flat keywords,
/// hierarchical keywords, and IPTC location fields. Handles both digiKam-style sidecars
/// (<c>digiKam:TagsList</c>) and generic XMP sidecars (<c>dc:subject</c>,
/// <c>lr:hierarchicalSubject</c>). The design principle is "tell the user to export to XMP
/// first; we read XMP" — we never touch native databases (digikam4.db, xnview.db, etc.).
///
/// Covers roadmap items M-03 (digiKam importer) and M-04 (XnView MP importer).
/// </summary>
public sealed class XmpSidecarImportService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(XmpSidecarImportService));

    // Standard XMP / RDF namespaces
    private static readonly XNamespace NsRdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace NsXmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace NsDc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace NsLr = "http://ns.adobe.com/lightroom/1.0/";
    private static readonly XNamespace NsDigiKam = "http://www.digikam.org/ns/1.0/";
    private static readonly XNamespace NsIptc = "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/";
    private static readonly XNamespace NsPhotoshop = "http://ns.adobe.com/photoshop/1.0/";

    public SidecarImportResult ImportSidecar(string sidecarPath)
    {
        if (string.IsNullOrWhiteSpace(sidecarPath))
            return SidecarImportResult.Failure("", "Sidecar path is empty.");

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(sidecarPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return SidecarImportResult.Failure(sidecarPath, "Sidecar path is not valid.");
        }

        if (!File.Exists(normalizedPath))
            return SidecarImportResult.Failure(normalizedPath, "Sidecar file does not exist.");

        try
        {
            var document = XDocument.Load(normalizedPath, LoadOptions.None);
            return ParseDocument(document, normalizedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException)
        {
            Log.LogWarning(ex, "Could not read XMP sidecar {Path}", normalizedPath);
            return SidecarImportResult.Failure(normalizedPath, "Sidecar could not be read.");
        }
    }

    public FolderImportResult ScanFolder(string folderPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return new FolderImportResult([], 0, 0, "Folder does not exist.");

        var results = new List<SidecarImportResult>();
        var success = 0;
        var failed = 0;

        var xmpFiles = Directory.EnumerateFiles(folderPath, "*.xmp", SearchOption.TopDirectoryOnly);
        foreach (var xmpPath in xmpFiles)
        {
            ct.ThrowIfCancellationRequested();
            var result = ImportSidecar(xmpPath);
            results.Add(result);
            if (result.Success) success++;
            else failed++;
        }

        var message = results.Count == 0
            ? "No XMP sidecar files found in this folder."
            : $"Scanned {results.Count} sidecar{(results.Count == 1 ? "" : "s")}: {success} imported, {failed} failed.";

        return new FolderImportResult(results, success, failed, message);
    }

    public SidecarImportResult ImportForImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return SidecarImportResult.Failure("", "No image path provided.");

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(imagePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return SidecarImportResult.Failure(imagePath, "Image path is not valid.");
        }

        foreach (var candidate in SidecarPaths(normalizedPath))
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                var document = XDocument.Load(candidate, LoadOptions.None);
                return ParseDocument(document, candidate);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException)
            {
                Log.LogWarning(ex, "Could not read XMP sidecar {Path}", candidate);
                return SidecarImportResult.Failure(candidate, "Sidecar could not be read.");
            }
        }

        return SidecarImportResult.Failure(
            SidecarPaths(normalizedPath).First(),
            "No XMP sidecar was found for this image.");
    }

    public static SidecarImportResult ParseDocument(XDocument document, string sidecarPath)
    {
        ArgumentNullException.ThrowIfNull(document);

        var rating = ReadRating(document);
        var colorLabel = ReadColorLabel(document);
        var flatKeywords = ReadFlatKeywords(document);
        var hierarchicalKeywords = ReadHierarchicalKeywords(document);
        var location = ReadLocation(document);

        var totalKeywords = flatKeywords.Count + hierarchicalKeywords.Count;
        var parts = new List<string>();
        if (rating is not null) parts.Add($"rating {rating}");
        if (colorLabel is not null) parts.Add($"label {colorLabel}");
        if (totalKeywords > 0) parts.Add($"{totalKeywords} keyword{Plural(totalKeywords)}");
        if (location.HasAnyField) parts.Add("location");

        var message = parts.Count == 0
            ? "Sidecar contains no importable metadata."
            : $"Imported: {string.Join(", ", parts)}.";

        return new SidecarImportResult(
            Success: true,
            SidecarPath: sidecarPath,
            Rating: rating,
            ColorLabel: colorLabel,
            FlatKeywords: flatKeywords,
            HierarchicalKeywords: hierarchicalKeywords,
            Message: message) { Location = location };
    }

    public static XmpFolderApplySummary ApplyFolderRatings(
        FolderImportResult result,
        Func<string, string?> findImageForSidecar,
        Action<string, int> applyRating)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(findImageForSidecar);
        ArgumentNullException.ThrowIfNull(applyRating);

        var ratingsApplied = 0;
        var skippedWithoutRating = 0;
        var unmatchedImages = 0;

        foreach (var item in result.Results)
        {
            if (!item.Success) continue;

            var imagePath = findImageForSidecar(item.SidecarPath);
            if (imagePath is null)
            {
                unmatchedImages++;
                continue;
            }

            if (item.Rating is int rating)
            {
                applyRating(imagePath, rating);
                ratingsApplied++;
            }
            else
            {
                skippedWithoutRating++;
            }
        }

        return new XmpFolderApplySummary(
            RatingsApplied: ratingsApplied,
            SkippedWithoutRating: skippedWithoutRating,
            UnmatchedImages: unmatchedImages,
            FailedSidecars: result.FailedCount);
    }

    internal static string? FindImageForSidecar(string sidecarPath)
    {
        var dir = Path.GetDirectoryName(sidecarPath);
        if (dir is null) return null;

        var xmpStem = Path.GetFileNameWithoutExtension(sidecarPath);
        if (string.IsNullOrWhiteSpace(xmpStem)) return null;

        var directCandidate = Path.Combine(dir, xmpStem);
        if (File.Exists(directCandidate) && IsSupportedImagePath(directCandidate))
            return directCandidate;

        var imageStem = Path.GetFileNameWithoutExtension(xmpStem);
        if (string.IsNullOrWhiteSpace(imageStem)) return null;

        foreach (var ext in DirectoryNavigator.SupportedExtensions)
        {
            var candidate = Path.Combine(dir, imageStem + ext);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static int? ReadRating(XDocument document)
    {
        // xmp:Rating as attribute on rdf:Description
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name == NsXmp + "Rating" ||
                (attribute.Name.Namespace == XNamespace.None &&
                 attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) &&
                 IsDescriptionElement(attribute.Parent)))
            {
                if (int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
                    return Math.Clamp(rating, -1, 5);
            }
        }

        // xmp:Rating as child element
        foreach (var element in document.Descendants())
        {
            if (element.Name == NsXmp + "Rating" ||
                element.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase))
            {
                if (!element.HasElements &&
                    int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
                {
                    return Math.Clamp(rating, -1, 5);
                }
            }
        }

        return null;
    }

    private static string? ReadColorLabel(XDocument document)
    {
        // xmp:Label as attribute on rdf:Description
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name == NsXmp + "Label" ||
                (attribute.Name.Namespace == XNamespace.None &&
                 attribute.Name.LocalName.Equals("Label", StringComparison.OrdinalIgnoreCase) &&
                 IsDescriptionElement(attribute.Parent)))
            {
                var label = NormalizeColorLabel(attribute.Value);
                if (label is not null) return label;
            }
        }

        // xmp:Label as child element
        foreach (var element in document.Descendants())
        {
            if (element.Name == NsXmp + "Label" ||
                element.Name.LocalName.Equals("Label", StringComparison.OrdinalIgnoreCase))
            {
                if (!element.HasElements)
                {
                    var label = NormalizeColorLabel(element.Value);
                    if (label is not null) return label;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadFlatKeywords(XDocument document)
    {
        var keywords = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        // dc:subject bag items
        foreach (var element in document.Descendants())
        {
            if (element.Name == NsDc + "subject" ||
                element.Name.LocalName.Equals("subject", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var li in element.Descendants()
                             .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
                {
                    var value = (li.Value ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        keywords.Add(value);
                }
            }
        }

        return keywords.ToList();
    }

    private static IReadOnlyList<string> ReadHierarchicalKeywords(XDocument document)
    {
        var keywords = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.Descendants())
        {
            var isHierarchical =
                element.Name == NsLr + "hierarchicalSubject" ||
                element.Name == NsDigiKam + "TagsList" ||
                element.Name.LocalName.Equals("hierarchicalSubject", StringComparison.OrdinalIgnoreCase) ||
                element.Name.LocalName.Equals("TagsList", StringComparison.OrdinalIgnoreCase);

            if (!isHierarchical)
                continue;

            foreach (var li in element.Descendants()
                         .Where(e => e.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
            {
                var value = (li.Value ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    keywords.Add(value);
            }
        }

        return keywords.ToList();
    }

    private static SidecarLocation ReadLocation(XDocument document)
    {
        string? location = null;
        string? city = null;
        string? state = null;
        string? country = null;

        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name == NsIptc + "Location" ||
                (attribute.Name.LocalName.Equals("Location", StringComparison.OrdinalIgnoreCase) &&
                 IsIptcContext(attribute)))
                location ??= TrimOrNull(attribute.Value);

            if (attribute.Name == NsPhotoshop + "City" ||
                attribute.Name.LocalName.Equals("City", StringComparison.OrdinalIgnoreCase))
                city ??= TrimOrNull(attribute.Value);

            if (attribute.Name == NsPhotoshop + "State" ||
                attribute.Name.LocalName.Equals("State-Province", StringComparison.OrdinalIgnoreCase))
                state ??= TrimOrNull(attribute.Value);

            if (attribute.Name == NsPhotoshop + "Country" ||
                attribute.Name.LocalName.Equals("Country", StringComparison.OrdinalIgnoreCase))
                country ??= TrimOrNull(attribute.Value);
        }

        foreach (var element in document.Descendants())
        {
            if (element.HasElements) continue;

            if (element.Name == NsIptc + "Location" ||
                element.Name.LocalName.Equals("Location", StringComparison.OrdinalIgnoreCase))
                location ??= TrimOrNull(element.Value);

            if (element.Name == NsPhotoshop + "City" ||
                element.Name.LocalName.Equals("City", StringComparison.OrdinalIgnoreCase))
                city ??= TrimOrNull(element.Value);

            if (element.Name == NsPhotoshop + "State" ||
                element.Name.LocalName.Equals("State-Province", StringComparison.OrdinalIgnoreCase))
                state ??= TrimOrNull(element.Value);

            if (element.Name == NsPhotoshop + "Country" ||
                element.Name.LocalName.Equals("Country", StringComparison.OrdinalIgnoreCase))
                country ??= TrimOrNull(element.Value);
        }

        return new SidecarLocation(location, city, state, country);
    }

    private static string? NormalizeColorLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Equals("Red", StringComparison.OrdinalIgnoreCase)) return "Red";
        if (trimmed.Equals("Yellow", StringComparison.OrdinalIgnoreCase)) return "Yellow";
        if (trimmed.Equals("Green", StringComparison.OrdinalIgnoreCase)) return "Green";
        if (trimmed.Equals("Blue", StringComparison.OrdinalIgnoreCase)) return "Blue";
        if (trimmed.Equals("Purple", StringComparison.OrdinalIgnoreCase)) return "Purple";

        // Some apps use numbers: 0=none, 1=Red, 2=Yellow, 3=Green, 4=Blue, 5=Purple
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric switch
            {
                1 => "Red",
                2 => "Yellow",
                3 => "Green",
                4 => "Blue",
                5 => "Purple",
                _ => null
            };
        }

        // Accept any non-empty string as a custom label
        return trimmed;
    }

    private static bool IsDescriptionElement(XElement? element)
        => element is not null &&
           element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase);

    private static bool IsIptcContext(XAttribute attribute)
    {
        var ns = attribute.Name.NamespaceName;
        return !string.IsNullOrEmpty(ns) &&
               ns.Contains("iptc", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool IsSupportedImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return DirectoryNavigator.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SidecarPaths(string imagePath)
    {
        yield return imagePath + ".xmp";

        var directory = Path.GetDirectoryName(imagePath);
        var basename = Path.GetFileNameWithoutExtension(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return Path.Combine(directory, basename + ".xmp");
    }

    private static string Plural(int count) => count == 1 ? "" : "s";
}

/// <summary>
/// Result of importing metadata from an XMP sidecar file.
/// </summary>
public sealed record SidecarImportResult(
    bool Success,
    string SidecarPath,
    int? Rating,
    string? ColorLabel,
    IReadOnlyList<string> FlatKeywords,
    IReadOnlyList<string> HierarchicalKeywords,
    string Message)
{
    /// <summary>Location fields parsed from IPTC/Photoshop namespaces.</summary>
    public SidecarLocation Location { get; init; } = SidecarLocation.Empty;

    /// <summary>All unique keywords (flat + leaf of hierarchical) merged and sorted.</summary>
    public IReadOnlyList<string> AllKeywords
    {
        get
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kw in FlatKeywords) set.Add(kw);
            foreach (var hk in HierarchicalKeywords)
            {
                // Hierarchical keywords are pipe- or slash-separated paths.
                // Add both the full path and the leaf segment.
                set.Add(hk);
                var leaf = ExtractLeaf(hk);
                if (!string.IsNullOrWhiteSpace(leaf))
                    set.Add(leaf);
            }
            return set.ToList();
        }
    }

    private static string ExtractLeaf(string hierarchicalKeyword)
    {
        // digiKam uses "/" separator, Lightroom uses "|" separator
        var lastSlash = hierarchicalKeyword.LastIndexOf('/');
        var lastPipe = hierarchicalKeyword.LastIndexOf('|');
        var separator = Math.Max(lastSlash, lastPipe);
        return separator >= 0 && separator < hierarchicalKeyword.Length - 1
            ? hierarchicalKeyword[(separator + 1)..].Trim()
            : hierarchicalKeyword.Trim();
    }

    internal static SidecarImportResult Failure(string sidecarPath, string message)
        => new(
            Success: false,
            SidecarPath: sidecarPath,
            Rating: null,
            ColorLabel: null,
            FlatKeywords: [],
            HierarchicalKeywords: [],
            Message: message);
}

public sealed record FolderImportResult(
    IReadOnlyList<SidecarImportResult> Results,
    int SuccessCount,
    int FailedCount,
    string Message)
{
    public int TotalScanned => Results.Count;
}

public sealed record XmpFolderApplySummary(
    int RatingsApplied,
    int SkippedWithoutRating,
    int UnmatchedImages,
    int FailedSidecars);

/// <summary>
/// IPTC/Photoshop location fields extracted from an XMP sidecar. All fields are informational.
/// </summary>
public sealed record SidecarLocation(
    string? Location,
    string? City,
    string? StateProvince,
    string? Country)
{
    public static readonly SidecarLocation Empty = new(null, null, null, null);

    public bool HasAnyField =>
        Location is not null || City is not null || StateProvince is not null || Country is not null;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Location is not null) parts.Add(Location);
        if (City is not null) parts.Add(City);
        if (StateProvince is not null) parts.Add(StateProvince);
        if (Country is not null) parts.Add(Country);
        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }
}
