using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record PicasaFaceRegion(
    string ContactId,
    string? Name,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record PicasaImageImportResult(
    bool Success,
    string ImagePath,
    string SidecarPath,
    int? Rating,
    IReadOnlyList<string> Albums,
    IReadOnlyList<PicasaFaceRegion> Faces,
    string Message);

public sealed record PicasaImportSummary(
    bool Success,
    string FolderPath,
    string IniPath,
    string? ContactsPath,
    IReadOnlyList<PicasaImageImportResult> Images,
    int SidecarsWritten,
    int RatingsWritten,
    int AlbumsWritten,
    int FacesWritten,
    int ContactsResolved,
    int MissingImages,
    string Message);

public sealed class PicasaImportService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(PicasaImportService));
    private static readonly Regex FaceRegex = new(
        @"rect64\(([0-9a-fA-F]{1,16})\)\s*,\s*([^;,\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Lr = "http://ns.adobe.com/lightroom/1.0/";
    private static readonly XNamespace MwgRs = "http://www.metadataworkinggroup.com/schemas/regions/";
    private static readonly XNamespace StArea = "http://ns.adobe.com/xmp/sType/Area#";
    private static readonly XNamespace Imv = "http://maven.imaging/1.0/";

    public PicasaImportSummary ImportFolder(
        string folderPath,
        string? contactsXmlPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return Failure("", "", contactsXmlPath, "Choose a Picasa folder before importing.");

        string folder;
        try
        {
            folder = Path.GetFullPath(folderPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return Failure(folderPath, "", contactsXmlPath, "Picasa folder path is not valid.");
        }

        if (!Directory.Exists(folder))
            return Failure(folder, "", contactsXmlPath, "Picasa folder does not exist.");

        var iniPath = Path.Combine(folder, ".picasa.ini");
        if (!File.Exists(iniPath))
            return Failure(folder, iniPath, contactsXmlPath, "No .picasa.ini file was found in this folder.");

        try
        {
            var contactsPath = ResolveContactsPath(folder, contactsXmlPath);
            var contacts = LoadContacts(contactsPath);
            var entries = ParseIni(iniPath);
            var albumNames = BuildAlbumNameMap(entries);
            var results = new List<PicasaImageImportResult>();
            var sidecarsWritten = 0;
            var ratingsWritten = 0;
            var albumsWritten = 0;
            var facesWritten = 0;
            var contactsResolved = 0;
            var missingImages = 0;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Real .picasa.ini files always carry structural sections —
                // [Picasa], [Contacts2], [encoding], [.album:<id>] — that are
                // not image references. Treating them as missing images floods
                // the results with false "not found" rows.
                if (IsNonFileSection(entry.FileName))
                    continue;

                try
                {
                if (!TryResolveImagePath(folder, entry.FileName, out var imagePath))
                {
                    missingImages++;
                    var skippedPath = Path.Combine(folder, entry.FileName);
                    results.Add(new PicasaImageImportResult(
                        false,
                        skippedPath,
                        skippedPath + ".xmp",
                        null,
                        [],
                        [],
                        "Image file referenced by .picasa.ini was outside the selected folder."));
                    continue;
                }

                var sidecarPath = imagePath + ".xmp";
                if (!File.Exists(imagePath))
                {
                    missingImages++;
                    results.Add(new PicasaImageImportResult(
                        false,
                        imagePath,
                        sidecarPath,
                        null,
                        [],
                        [],
                        "Image file referenced by .picasa.ini was not found."));
                    continue;
                }

                var rating = ReadRating(entry.Values);
                var albums = ReadAlbums(entry.Values, albumNames);
                var faces = ReadFaces(entry.Values, contacts);
                var hasMetadata = rating is not null || albums.Count > 0 || faces.Count > 0;
                if (!hasMetadata)
                {
                    results.Add(new PicasaImageImportResult(
                        true,
                        imagePath,
                        sidecarPath,
                        null,
                        [],
                        [],
                        "No importable Picasa metadata was found for this image."));
                    continue;
                }

                WriteSidecar(imagePath, rating, albums, faces);
                sidecarsWritten++;
                if (rating is not null) ratingsWritten++;
                albumsWritten += albums.Count;
                facesWritten += faces.Count;
                contactsResolved += faces.Count(face => !string.IsNullOrWhiteSpace(face.Name));
                results.Add(new PicasaImageImportResult(
                    true,
                    imagePath,
                    sidecarPath,
                    rating,
                    albums,
                    faces,
                    $"Wrote Picasa metadata sidecar with {DescribeImageMetadata(rating, albums.Count, faces.Count)}."));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or ArgumentException or NotSupportedException)
                {
                    // One unreadable sidecar (foreign tool, malformed XML) must
                    // not discard every other image's result; record it and
                    // continue so partial imports still report progress.
                    var failedPath = Path.Combine(folder, entry.FileName);
                    Log.LogWarning(ex, "Could not import Picasa metadata for {Path}", failedPath);
                    results.Add(new PicasaImageImportResult(
                        false,
                        failedPath,
                        failedPath + ".xmp",
                        null,
                        [],
                        [],
                        $"Could not write metadata sidecar: {ex.Message}"));
                }
            }

            var message = sidecarsWritten == 0
                ? $"No Picasa metadata sidecars were written. {missingImages} missing image{Plural(missingImages)}."
                : $"Imported Picasa metadata for {sidecarsWritten} image{Plural(sidecarsWritten)}: {ratingsWritten} rating{Plural(ratingsWritten)}, {albumsWritten} album tag{Plural(albumsWritten)}, {facesWritten} face region{Plural(facesWritten)}.";

            return new PicasaImportSummary(
                Success: sidecarsWritten > 0,
                FolderPath: folder,
                IniPath: iniPath,
                ContactsPath: contactsPath,
                Images: results,
                SidecarsWritten: sidecarsWritten,
                RatingsWritten: ratingsWritten,
                AlbumsWritten: albumsWritten,
                FacesWritten: facesWritten,
                ContactsResolved: contactsResolved,
                MissingImages: missingImages,
                Message: message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not import Picasa metadata from {Folder}", folder);
            return Failure(folder, iniPath, contactsXmlPath, "Picasa metadata could not be imported.");
        }
    }

    internal static IReadOnlyList<PicasaIniEntry> ParseIni(string iniPath)
    {
        var entries = new List<PicasaIniEntry>();
        PicasaIniEntryBuilder? current = null;

        foreach (var rawLine in File.ReadLines(iniPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                if (current is not null)
                    entries.Add(current.Build());
                current = new PicasaIniEntryBuilder(line[1..^1].Trim());
                continue;
            }

            if (current is null)
                continue;

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length > 0)
                current.Values[key] = value;
        }

        if (current is not null)
            entries.Add(current.Build());

        return entries;
    }

    internal static IReadOnlyDictionary<string, string> LoadContacts(string? contactsXmlPath)
    {
        if (string.IsNullOrWhiteSpace(contactsXmlPath) || !File.Exists(contactsXmlPath))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var document = BoundedXmlReader.Load(contactsXmlPath, LoadOptions.None);
        var contacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contact in document.Descendants().Where(element => element.Name.LocalName.Equals("contact", StringComparison.OrdinalIgnoreCase)))
        {
            var id = AttributeValue(contact, "id") ?? AttributeValue(contact, "contactid") ?? AttributeValue(contact, "hash");
            var name = AttributeValue(contact, "name") ?? AttributeValue(contact, "display") ?? AttributeValue(contact, "displayName");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                contacts[id.Trim()] = name.Trim();
        }

        return contacts;
    }

    internal static IReadOnlyList<PicasaFaceRegion> ReadFaces(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> contacts)
    {
        if (!values.TryGetValue("faces", out var rawFaces) || string.IsNullOrWhiteSpace(rawFaces))
            return [];

        var faces = new List<PicasaFaceRegion>();
        foreach (Match match in FaceRegex.Matches(rawFaces))
        {
            if (!TryParseRect64(match.Groups[1].Value, out var x, out var y, out var width, out var height))
                continue;

            var contactId = match.Groups[2].Value.Trim();
            contacts.TryGetValue(contactId, out var name);
            faces.Add(new PicasaFaceRegion(contactId, name, x, y, width, height));
        }

        return faces;
    }

    private static PicasaImportSummary Failure(string folderPath, string iniPath, string? contactsPath, string message)
        => new(
            Success: false,
            FolderPath: folderPath,
            IniPath: iniPath,
            ContactsPath: contactsPath,
            Images: [],
            SidecarsWritten: 0,
            RatingsWritten: 0,
            AlbumsWritten: 0,
            FacesWritten: 0,
            ContactsResolved: 0,
            MissingImages: 0,
            Message: message);

    private static string? ResolveContactsPath(string folder, string? contactsXmlPath)
    {
        if (!string.IsNullOrWhiteSpace(contactsXmlPath))
            return Path.GetFullPath(contactsXmlPath);

        var local = Path.Combine(folder, "contacts.xml");
        if (File.Exists(local))
            return local;

        var parent = Directory.GetParent(folder);
        if (parent is not null)
        {
            var parentContacts = Path.Combine(parent.FullName, "contacts.xml");
            if (File.Exists(parentContacts))
                return parentContacts;
        }

        return null;
    }

    private static bool TryResolveImagePath(string folder, string fileName, out string imagePath)
    {
        imagePath = "";
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
            return false;

        var folderRoot = Path.GetFullPath(folder);
        var candidate = Path.GetFullPath(Path.Combine(folderRoot, fileName));
        var folderPrefix = folderRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        imagePath = candidate;
        return true;
    }

    private static int? ReadRating(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue("rating", out var ratingRaw) &&
            int.TryParse(ratingRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
        {
            return Math.Clamp(rating, -1, 5);
        }

        if (values.TryGetValue("star", out var starRaw) &&
            IsTrue(starRaw))
        {
            return 5;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadAlbums(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> albumNames)
    {
        var albums = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "albums", "album" })
        {
            if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            foreach (var album in SplitList(raw))
            {
                // Picasa records album IDs here; the display name lives in the
                // matching [.album:<id>] section. Prefer the resolved name.
                albums.Add(albumNames.TryGetValue(album, out var name) ? name : album);
            }
        }

        return albums.ToList();
    }

    private static IReadOnlyDictionary<string, string> BuildAlbumNameMap(IReadOnlyList<PicasaIniEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!entry.FileName.StartsWith(".album:", StringComparison.OrdinalIgnoreCase))
                continue;

            var id = entry.FileName[".album:".Length..];
            if (id.Length > 0 && entry.Values.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
                map[id] = name.Trim();
        }

        return map;
    }

    private static bool IsNonFileSection(string sectionName)
        => sectionName.Equals("Picasa", StringComparison.OrdinalIgnoreCase)
            || sectionName.Equals("Contacts", StringComparison.OrdinalIgnoreCase)
            || sectionName.Equals("Contacts2", StringComparison.OrdinalIgnoreCase)
            || sectionName.Equals("encoding", StringComparison.OrdinalIgnoreCase)
            || sectionName.StartsWith(".album:", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitList(string raw)
        => raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim().Trim('"'))
            .Where(value => value.Length > 0);

    private static bool IsTrue(string value)
        => value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("1", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseRect64(string hex, out double x, out double y, out double width, out double height)
    {
        x = y = width = height = 0;
        if (hex.Length is < 1 or > 16)
            return false;

        try
        {
            hex = hex.PadLeft(16, '0');
            var left = Convert.ToInt32(hex[..4], 16);
            var top = Convert.ToInt32(hex[4..8], 16);
            var right = Convert.ToInt32(hex[8..12], 16);
            var bottom = Convert.ToInt32(hex[12..16], 16);
            if (right <= left || bottom <= top)
                return false;

            x = Clamp01((left + right) / 2d / ushort.MaxValue);
            y = Clamp01((top + bottom) / 2d / ushort.MaxValue);
            width = Clamp01((right - left) / (double)ushort.MaxValue);
            height = Clamp01((bottom - top) / (double)ushort.MaxValue);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void WriteSidecar(
        string imagePath,
        int? rating,
        IReadOnlyList<string> albums,
        IReadOnlyList<PicasaFaceRegion> faces)
    {
        var sidecarPath = imagePath + ".xmp";
        var document = File.Exists(sidecarPath)
            ? BoundedXmlReader.Load(sidecarPath, LoadOptions.None)
            : CreateEmptySidecar();
        var description = EnsureDescription(document);
        EnsureNamespaces(document);

        if (rating is not null)
            description.SetAttributeValue(Xmp + "Rating", rating.Value.ToString(CultureInfo.InvariantCulture));

        var faceNames = faces
            .Select(face => face.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => "person:" + name!.Trim());
        var flatTags = albums.Select(album => "album:" + album).Concat(faceNames);
        ReplaceArray(description, Dc + "subject", Rdf + "Bag", MergeExisting(description, Dc + "subject", flatTags));

        var hierarchicalTags = albums
            .Select(album => "Picasa|Albums|" + album)
            .Concat(faces
                .Where(face => !string.IsNullOrWhiteSpace(face.Name))
                .Select(face => "Picasa|People|" + face.Name!.Trim()));
        ReplaceArray(description, Lr + "hierarchicalSubject", Rdf + "Bag", MergeExisting(description, Lr + "hierarchicalSubject", hierarchicalTags));

        if (faces.Count > 0)
            ReplaceRegions(description, faces);

        description.SetAttributeValue(Imv + "PicasaImportedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        SidecarWriter.SaveAtomically(document, sidecarPath);
    }

    private static IReadOnlyList<string> MergeExisting(XElement description, XName arrayName, IEnumerable<string> additions)
    {
        var merged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = description.Element(arrayName);
        if (existing is not null)
        {
            foreach (var item in existing.Descendants().Where(element => element.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
            {
                var value = item.Value.Trim();
                if (value.Length > 0)
                    merged.Add(value);
            }
        }

        foreach (var value in additions)
        {
            var trimmed = value.Trim();
            if (trimmed.Length > 0)
                merged.Add(trimmed);
        }

        return merged.ToList();
    }

    private static void ReplaceArray(XElement description, XName name, XName arrayType, IReadOnlyList<string> values)
    {
        description.Elements(name).Remove();
        if (values.Count == 0)
            return;

        description.Add(new XElement(name, new XElement(arrayType, values.Select(value => new XElement(Rdf + "li", value)))));
    }

    private static void ReplaceRegions(XElement description, IReadOnlyList<PicasaFaceRegion> faces)
    {
        description.Elements(MwgRs + "Regions").Remove();
        description.Add(
            new XElement(MwgRs + "Regions",
                new XAttribute(Rdf + "parseType", "Resource"),
                new XElement(MwgRs + "RegionList",
                    new XElement(Rdf + "Bag",
                        faces.Select(face =>
                            new XElement(Rdf + "li",
                                new XAttribute(Rdf + "parseType", "Resource"),
                                new XElement(MwgRs + "Name", string.IsNullOrWhiteSpace(face.Name) ? face.ContactId : face.Name),
                                new XElement(MwgRs + "Type", "Face"),
                                new XElement(MwgRs + "Area",
                                    new XAttribute(Rdf + "parseType", "Resource"),
                                    new XAttribute(StArea + "x", FormatUnit(face.X)),
                                    new XAttribute(StArea + "y", FormatUnit(face.Y)),
                                    new XAttribute(StArea + "w", FormatUnit(face.Width)),
                                    new XAttribute(StArea + "h", FormatUnit(face.Height)),
                                    new XAttribute(StArea + "unit", "normalized")),
                                new XElement(Imv + "PicasaContactId", face.ContactId)))))));
    }

    private static XElement EnsureDescription(XDocument document)
    {
        var root = document.Root;
        if (root is null)
        {
            root = CreateEmptySidecar().Root!;
            document.Add(root);
        }

        var rdfRoot = document.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("RDF", StringComparison.OrdinalIgnoreCase));
        if (rdfRoot is null)
        {
            rdfRoot = new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName));
            root.Add(rdfRoot);
        }

        var description = rdfRoot.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (description is not null)
            return description;

        description = new XElement(Rdf + "Description");
        rdfRoot.Add(description);
        return description;
    }

    private static XDocument CreateEmptySidecar()
        => new(
            new XElement(
                X + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                    new XElement(Rdf + "Description"))));

    private static void EnsureNamespaces(XDocument document)
    {
        var root = document.Root;
        if (root is null)
            return;

        foreach (var item in new (string Prefix, XNamespace Namespace)[]
                 {
                     ("x", X),
                     ("xmp", Xmp),
                     ("dc", Dc),
                     ("lr", Lr),
                     ("mwg-rs", MwgRs),
                     ("stArea", StArea),
                     ("imv", Imv),
                 })
        {
            var attributeName = XNamespace.Xmlns + item.Prefix;
            if (root.Attribute(attributeName) is null)
                root.SetAttributeValue(attributeName, item.Namespace.NamespaceName);
        }
    }

    private static string? AttributeValue(XElement element, string localName)
        => element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static string FormatUnit(double value)
        => Clamp01(value).ToString("0.######", CultureInfo.InvariantCulture);

    private static double Clamp01(double value)
        => Math.Min(1d, Math.Max(0d, value));

    private static string DescribeImageMetadata(int? rating, int albumCount, int faceCount)
    {
        var parts = new List<string>();
        if (rating is not null) parts.Add("rating");
        if (albumCount > 0) parts.Add($"{albumCount} album tag{Plural(albumCount)}");
        if (faceCount > 0) parts.Add($"{faceCount} face region{Plural(faceCount)}");
        return parts.Count == 0 ? "no metadata" : string.Join(", ", parts);
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    internal sealed record PicasaIniEntry(string FileName, IReadOnlyDictionary<string, string> Values);

    private sealed class PicasaIniEntryBuilder(string fileName)
    {
        public string FileName { get; } = fileName;
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public PicasaIniEntry Build() => new(FileName, new Dictionary<string, string>(Values, StringComparer.OrdinalIgnoreCase));
    }
}
