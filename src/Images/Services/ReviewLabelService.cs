using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public enum ReviewLabelKind
{
    None,
    Pick,
    Reject
}

public sealed record ReviewLabelState(
    int? Rating,
    ReviewLabelKind Label,
    string SidecarPath)
{
    public string? ColorLabel { get; init; }
    public SidecarLocation Location { get; init; } = SidecarLocation.Empty;

    public string RatingText => Rating is null ? "Unrated" : $"{Rating} star{(Rating == 1 ? "" : "s")}";
    public string LabelText => Label switch
    {
        ReviewLabelKind.Pick => "Pick",
        ReviewLabelKind.Reject => "Reject",
        _ => "No label"
    };
}

public sealed record ReviewLabelMutationResult(
    bool Success,
    string SidecarPath,
    ReviewLabelState Previous,
    ReviewLabelState Current,
    string Message);

public sealed class ReviewLabelService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ReviewLabelService));

    public ReviewLabelState ReadState(string imagePath)
    {
        if (!TryNormalizeImagePath(imagePath, requireExistingImage: false, out var normalizedPath, out _))
            return new ReviewLabelState(null, ReviewLabelKind.None, "");

        var sidecarPath = SidecarPaths(normalizedPath).First();
        foreach (var candidate in SidecarPaths(normalizedPath))
        {
            if (!File.Exists(candidate))
                continue;

            sidecarPath = candidate;
            try
            {
                var document = XDocument.Load(candidate, LoadOptions.None);
                return new ReviewLabelState(ReadRating(document), ReadLabel(document), candidate)
                {
                    ColorLabel = ReadColorLabel(document),
                    Location = ReadLocation(document)
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or InvalidOperationException)
            {
                Log.LogWarning(ex, "Could not read review sidecar {Path}", candidate);
                return new ReviewLabelState(null, ReviewLabelKind.None, candidate);
            }
        }

        return new ReviewLabelState(null, ReviewLabelKind.None, sidecarPath);
    }

    public ReviewLabelMutationResult SetRating(string imagePath, int? rating)
    {
        rating = rating is null ? null : Math.Clamp(rating.Value, 0, 5);
        return Mutate(
            imagePath,
            state => state with { Rating = rating },
            rating is null ? "Rating cleared." : $"Rated {rating} star{(rating == 1 ? "" : "s")}.");
    }

    public ReviewLabelMutationResult SetLabel(string imagePath, ReviewLabelKind label)
        => Mutate(
            imagePath,
            state => state with { Label = label },
            label switch
            {
                ReviewLabelKind.Pick => "Marked pick.",
                ReviewLabelKind.Reject => "Marked reject.",
                _ => "Review label cleared."
            });

    public ReviewLabelMutationResult SetColorLabel(string imagePath, string? colorLabel)
        => Mutate(
            imagePath,
            state => state with { ColorLabel = colorLabel },
            colorLabel is null ? "Color label cleared." : $"Color label set to {colorLabel}.");

    public ReviewLabelMutationResult SetLocation(string imagePath, SidecarLocation location)
        => Mutate(
            imagePath,
            state => state with { Location = location },
            location.HasAnyField ? $"Location set to {location}." : "Location cleared.");

    public ReviewLabelMutationResult Restore(string imagePath, ReviewLabelState state)
        => Mutate(imagePath, _ => state, "Review label restored.");

    private static ReviewLabelMutationResult Mutate(
        string imagePath,
        Func<ReviewLabelState, ReviewLabelState> mutate,
        string successMessage)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (!TryNormalizeImagePath(imagePath, requireExistingImage: true, out var normalizedPath, out var pathError))
        {
            var empty = new ReviewLabelState(null, ReviewLabelKind.None, "");
            return new ReviewLabelMutationResult(false, "", empty, empty, pathError);
        }

        var service = new ReviewLabelService();
        var previous = service.ReadState(normalizedPath);
        var current = mutate(previous) with { SidecarPath = previous.SidecarPath };
        var sidecarPath = previous.SidecarPath;

        try
        {
            var document = File.Exists(sidecarPath)
                ? XDocument.Load(sidecarPath, LoadOptions.None)
                : CreateEmptySidecar();
            WriteState(document, current);
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            document.Save(sidecarPath);

            return new ReviewLabelMutationResult(true, sidecarPath, previous, current, successMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or InvalidOperationException)
        {
            Log.LogWarning(ex, "Could not write review sidecar {Path}", sidecarPath);
            return new ReviewLabelMutationResult(false, sidecarPath, previous, previous, "Review sidecar could not be written.");
        }
    }

    private static void WriteState(XDocument document, ReviewLabelState state)
    {
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        XNamespace imv = "http://maven.imaging/1.0/";
        XNamespace photoshop = "http://ns.adobe.com/photoshop/1.0/";
        XNamespace iptcCore = "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/";
        var description = EnsureDescription(document);
        EnsureNamespace(document, "xmp", xmp);
        EnsureNamespace(document, "imv", imv);

        description.SetAttributeValue(xmp + "Rating", state.Rating is null
            ? null
            : state.Rating.Value.ToString(CultureInfo.InvariantCulture));
        description.SetAttributeValue(imv + "ReviewLabel", state.Label == ReviewLabelKind.None
            ? null
            : state.Label.ToString().ToLowerInvariant());
        description.SetAttributeValue(xmp + "Label", string.IsNullOrWhiteSpace(state.ColorLabel)
            ? null
            : state.ColorLabel);

        if (state.Location.HasAnyField)
        {
            EnsureNamespace(document, "photoshop", photoshop);
            EnsureNamespace(document, "Iptc4xmpCore", iptcCore);
            description.SetAttributeValue(photoshop + "City", state.Location.City);
            description.SetAttributeValue(photoshop + "State", state.Location.StateProvince);
            description.SetAttributeValue(photoshop + "Country", state.Location.Country);
            description.SetAttributeValue(iptcCore + "Location", state.Location.Location);
        }
    }

    private static int? ReadRating(XDocument document)
    {
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
            {
                return Math.Clamp(rating, 0, 5);
            }
        }

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
            {
                return Math.Clamp(rating, 0, 5);
            }
        }

        return null;
    }

    private static ReviewLabelKind ReadLabel(XDocument document)
    {
        foreach (var value in document.Descendants().Attributes()
                     .Where(attribute => attribute.Name.LocalName.Equals("ReviewLabel", StringComparison.OrdinalIgnoreCase))
                     .Select(attribute => attribute.Value)
                     .Concat(document.Descendants()
                         .Where(element => element.Name.LocalName.Equals("ReviewLabel", StringComparison.OrdinalIgnoreCase))
                         .Select(element => element.Value)))
        {
            if (value.Equals("pick", StringComparison.OrdinalIgnoreCase))
                return ReviewLabelKind.Pick;
            if (value.Equals("reject", StringComparison.OrdinalIgnoreCase))
                return ReviewLabelKind.Reject;
        }

        return ReviewLabelKind.None;
    }

    private static string? ReadColorLabel(XDocument document)
    {
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name == xmp + "Label" ||
                (attribute.Name.LocalName.Equals("Label", StringComparison.OrdinalIgnoreCase) &&
                 attribute.Parent?.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase) == true))
            {
                var value = attribute.Value?.Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        return null;
    }

    private static SidecarLocation ReadLocation(XDocument document)
    {
        XNamespace photoshop = "http://ns.adobe.com/photoshop/1.0/";
        XNamespace iptcCore = "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/";
        string? location = null, city = null, state = null, country = null;

        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name == iptcCore + "Location")
                location ??= TrimOrNull(attribute.Value);
            if (attribute.Name == photoshop + "City")
                city ??= TrimOrNull(attribute.Value);
            if (attribute.Name == photoshop + "State")
                state ??= TrimOrNull(attribute.Value);
            if (attribute.Name == photoshop + "Country")
                country ??= TrimOrNull(attribute.Value);
        }
        return new SidecarLocation(location, city, state, country);
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static XElement EnsureDescription(XDocument document)
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

        var root = document.Root;
        if (root is null)
        {
            root = CreateEmptySidecar().Root!;
            document.Add(root);
        }

        var rdfRoot = document.Descendants().FirstOrDefault(
            element => element.Name.LocalName.Equals("RDF", StringComparison.OrdinalIgnoreCase));
        if (rdfRoot is null)
        {
            rdfRoot = new XElement(
                rdf + "RDF",
                new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName));
            root.Add(rdfRoot);
        }

        var description = rdfRoot.Elements().FirstOrDefault(
            element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (description is not null)
            return description;

        description = new XElement(rdf + "Description");
        rdfRoot.Add(description);
        return description;
    }

    private static void EnsureNamespace(XDocument document, string prefix, XNamespace ns)
    {
        var root = document.Root;
        if (root is null)
            return;

        var attributeName = XNamespace.Xmlns + prefix;
        if (root.Attribute(attributeName) is null)
            root.SetAttributeValue(attributeName, ns.NamespaceName);
    }

    private static XDocument CreateEmptySidecar()
    {
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        XNamespace imv = "http://maven.imaging/1.0/";

        return new XDocument(
            new XElement(
                x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xmp", xmp.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "imv", imv.NamespaceName),
                new XElement(
                    rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                    new XElement(rdf + "Description"))));
    }

    private static IEnumerable<string> SidecarPaths(string imagePath)
    {
        yield return imagePath + ".xmp";

        var directory = Path.GetDirectoryName(imagePath);
        var basename = Path.GetFileNameWithoutExtension(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return Path.Combine(directory, basename + ".xmp");
    }

    private static bool TryNormalizeImagePath(
        string imagePath,
        bool requireExistingImage,
        out string normalized,
        out string error)
    {
        normalized = "";
        error = "";

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            error = "Select an image first.";
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(imagePath);
            if (requireExistingImage && !File.Exists(normalized))
            {
                error = "Image file no longer exists.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = "Selected image path is not valid.";
            return false;
        }
    }
}
