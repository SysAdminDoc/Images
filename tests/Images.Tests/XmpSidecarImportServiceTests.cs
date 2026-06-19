using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class XmpSidecarImportServiceTests
{
    private readonly XmpSidecarImportService _service = new();

    [Fact]
    public void ImportSidecar_ReturnsFailure_WhenPathEmpty()
    {
        var result = _service.ImportSidecar("");
        Assert.False(result.Success);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportSidecar_ReturnsFailure_WhenFileDoesNotExist()
    {
        using var temp = TestDirectory.Create();
        var result = _service.ImportSidecar(Path.Combine(temp.Path, "missing.xmp"));
        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportSidecar_ReturnsFailure_WhenXmlMalformed()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("bad.xmp", "<<<not xml>>>");
        var result = _service.ImportSidecar(xmpPath);
        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportSidecar_ParsesRatingAsAttribute()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("rated.xmp", BuildSidecar(ratingAttr: "4"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(4, result.Rating);
    }

    [Fact]
    public void ImportSidecar_ParsesRatingAsElement()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("rated.xmp", BuildSidecar(ratingElement: "3"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(3, result.Rating);
    }

    [Fact]
    public void ImportSidecar_ClampsRating_RejectMinusOne()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("reject.xmp", BuildSidecar(ratingAttr: "-1"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(-1, result.Rating);
    }

    [Fact]
    public void ImportSidecar_ClampsRating_AboveFiveBecomesFive()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("over.xmp", BuildSidecar(ratingAttr: "99"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(5, result.Rating);
    }

    [Fact]
    public void ImportSidecar_ParsesColorLabelString()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("label.xmp", BuildSidecar(labelAttr: "Red"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal("Red", result.ColorLabel);
    }

    [Fact]
    public void ImportSidecar_ParsesColorLabelCaseInsensitive()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("label.xmp", BuildSidecar(labelAttr: "YELLOW"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal("Yellow", result.ColorLabel);
    }

    [Fact]
    public void ImportSidecar_ParsesFlatKeywords_DcSubject()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("tags.xmp", BuildSidecar(
            dcSubjectItems: ["landscape", "sunset", "mountains"]));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(["landscape", "mountains", "sunset"], result.FlatKeywords);
    }

    [Fact]
    public void ImportSidecar_ParsesHierarchicalKeywords_LightroomStyle()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("hier.xmp", BuildSidecar(
            lrHierarchicalItems: ["Places|Europe|France", "People|Alice"]));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(["People|Alice", "Places|Europe|France"], result.HierarchicalKeywords);
    }

    [Fact]
    public void ImportSidecar_ParsesHierarchicalKeywords_DigiKamStyle()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("dk.xmp", BuildSidecar(
            digiKamTagsItems: ["Places/Europe/France", "People/Bob"]));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(["People/Bob", "Places/Europe/France"], result.HierarchicalKeywords);
    }

    [Fact]
    public void ImportSidecar_ParsesLocation()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("loc.xmp", BuildSidecar(
            city: "Paris", stateProvince: "Ile-de-France", country: "France"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal("Paris", result.Location.City);
        Assert.Equal("Ile-de-France", result.Location.StateProvince);
        Assert.Equal("France", result.Location.Country);
        Assert.True(result.Location.HasAnyField);
    }

    [Fact]
    public void ImportSidecar_FullDigiKamSidecar_ParsesAllFields()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("digikam-full.xmp", BuildSidecar(
            ratingAttr: "5",
            labelAttr: "Green",
            dcSubjectItems: ["vacation", "family"],
            digiKamTagsItems: ["Events/Vacation/Summer 2026", "People/Alice"],
            city: "Nice",
            country: "France"));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Green", result.ColorLabel);
        Assert.Equal(["family", "vacation"], result.FlatKeywords);
        Assert.Equal(["Events/Vacation/Summer 2026", "People/Alice"], result.HierarchicalKeywords);
        Assert.Equal("Nice", result.Location.City);
        Assert.Equal("France", result.Location.Country);
    }

    [Fact]
    public void ImportSidecar_FullXnViewSidecar_ParsesAllFields()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("xnview-full.xmp", BuildSidecar(
            ratingAttr: "3",
            labelAttr: "Blue",
            dcSubjectItems: ["architecture", "bridge"],
            lrHierarchicalItems: ["Places|Europe|UK|London"]));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Equal(3, result.Rating);
        Assert.Equal("Blue", result.ColorLabel);
        Assert.Equal(["architecture", "bridge"], result.FlatKeywords);
        Assert.Equal(["Places|Europe|UK|London"], result.HierarchicalKeywords);
    }

    [Fact]
    public void ImportSidecar_EmptySidecar_SucceedsWithNoMetadata()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("empty.xmp", BuildSidecar());
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        Assert.Null(result.Rating);
        Assert.Null(result.ColorLabel);
        Assert.Empty(result.FlatKeywords);
        Assert.Empty(result.HierarchicalKeywords);
        Assert.False(result.Location.HasAnyField);
        Assert.Contains("no importable metadata", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportForImage_FindsSidecarWithExtensionAppended()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "photo.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8]);
        temp.WriteFile("photo.jpg.xmp", BuildSidecar(ratingAttr: "2"));

        var result = _service.ImportForImage(imagePath);

        Assert.True(result.Success);
        Assert.Equal(2, result.Rating);
    }

    [Fact]
    public void ImportForImage_FindsSidecarWithExtensionReplaced()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "photo.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8]);
        temp.WriteFile("photo.xmp", BuildSidecar(ratingAttr: "1", labelAttr: "Purple"));

        var result = _service.ImportForImage(imagePath);

        Assert.True(result.Success);
        Assert.Equal(1, result.Rating);
        Assert.Equal("Purple", result.ColorLabel);
    }

    [Fact]
    public void FindImageForSidecar_FindsImageWhenXmpExtensionIsAppended()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "photo.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8]);
        var sidecarPath = temp.WriteFile("photo.jpg.xmp", BuildSidecar(ratingAttr: "2"));

        var found = XmpSidecarImportService.FindImageForSidecar(sidecarPath);

        Assert.Equal(imagePath, found);
    }

    [Fact]
    public void FindImageForSidecar_FindsImageWhenXmpExtensionReplacesImageExtension()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "photo.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8]);
        var sidecarPath = temp.WriteFile("photo.xmp", BuildSidecar(ratingAttr: "2"));

        var found = XmpSidecarImportService.FindImageForSidecar(sidecarPath);

        Assert.Equal(imagePath, found);
    }

    [Fact]
    public void ApplyFolderRatings_ReportsAppliedSkippedUnmatchedAndFailedCounts()
    {
        var applied = new List<(string Path, int Rating)>();
        var result = new FolderImportResult(
            [
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "rated.xmp",
                    Rating: 4,
                    ColorLabel: null,
                    FlatKeywords: [],
                    HierarchicalKeywords: [],
                    Message: "rated"),
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "keywords-only.xmp",
                    Rating: null,
                    ColorLabel: null,
                    FlatKeywords: ["portrait"],
                    HierarchicalKeywords: [],
                    Message: "keywords"),
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "unmatched.xmp",
                    Rating: 5,
                    ColorLabel: null,
                    FlatKeywords: [],
                    HierarchicalKeywords: [],
                    Message: "unmatched"),
                SidecarImportResult.Failure("bad.xmp", "bad")
            ],
            SuccessCount: 3,
            FailedCount: 1,
            Message: "scan");

        var summary = XmpSidecarImportService.ApplyFolderRatings(
            result,
            sidecar => sidecar switch
            {
                "rated.xmp" => "photo.jpg",
                "keywords-only.xmp" => "keywords.jpg",
                _ => null
            },
            (path, rating) => applied.Add((path, rating)));

        Assert.Equal(new XmpFolderApplySummary(
            RatingsApplied: 1,
            SkippedWithoutRating: 1,
            UnmatchedImages: 1,
            FailedSidecars: 1), summary);
        var call = Assert.Single(applied);
        Assert.Equal(("photo.jpg", 4), call);
    }

    [Fact]
    public void ImportForImage_ReturnsFailure_WhenNoSidecarExists()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "nosidecar.jpg");
        File.WriteAllBytes(imagePath, [0xFF, 0xD8]);

        var result = _service.ImportForImage(imagePath);

        Assert.False(result.Success);
        Assert.Contains("No XMP sidecar", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllKeywords_MergesFlatAndHierarchicalLeaves()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("merge.xmp", BuildSidecar(
            dcSubjectItems: ["sunset", "beach"],
            lrHierarchicalItems: ["Places|Europe|France", "Events|Vacation"]));
        var result = _service.ImportSidecar(xmpPath);

        Assert.True(result.Success);
        var all = result.AllKeywords;
        // Should contain flat keywords + full hierarchical paths + leaf segments
        Assert.Contains("sunset", all);
        Assert.Contains("beach", all);
        Assert.Contains("Places|Europe|France", all);
        Assert.Contains("France", all);
        Assert.Contains("Events|Vacation", all);
        Assert.Contains("Vacation", all);
    }

    [Fact]
    public void AllKeywords_ExtractsLeafFromDigiKamSlashSeparated()
    {
        using var temp = TestDirectory.Create();
        var xmpPath = temp.WriteFile("leaf.xmp", BuildSidecar(
            digiKamTagsItems: ["People/Family/Alice"]));
        var result = _service.ImportSidecar(xmpPath);

        var all = result.AllKeywords;
        Assert.Contains("People/Family/Alice", all);
        Assert.Contains("Alice", all);
    }

    [Fact]
    public void SidecarLocation_ToString_JoinsNonNullFields()
    {
        var loc = new SidecarLocation("Main St", "Springfield", "IL", "USA");
        Assert.Equal("Main St, Springfield, IL, USA", loc.ToString());

        var partial = new SidecarLocation(null, "Tokyo", null, "Japan");
        Assert.Equal("Tokyo, Japan", partial.ToString());

        Assert.Equal("", SidecarLocation.Empty.ToString());
    }

    [Fact]
    public void ParseDocument_WorksDirectly_WithXDocument()
    {
        var doc = XDocument.Parse(BuildSidecar(ratingAttr: "4", labelAttr: "Yellow",
            dcSubjectItems: ["test"]));
        var result = XmpSidecarImportService.ParseDocument(doc, "test.xmp");

        Assert.True(result.Success);
        Assert.Equal(4, result.Rating);
        Assert.Equal("Yellow", result.ColorLabel);
        Assert.Equal(["test"], result.FlatKeywords);
    }

    [Fact]
    public void ApplyFolder_WritesRatingsLabelsKeywordsAndLocations()
    {
        var ratings = new List<(string Path, int Rating)>();
        var labels = new List<(string Path, string Label)>();
        var keywords = new List<(string Path, IReadOnlyList<string> Keywords)>();
        var locations = new List<(string Path, SidecarLocation Location)>();

        var result = new FolderImportResult(
            [
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "full.xmp",
                    Rating: 5,
                    ColorLabel: "Green",
                    FlatKeywords: ["landscape"],
                    HierarchicalKeywords: [],
                    Message: "full"),
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "loc.xmp",
                    Rating: null,
                    ColorLabel: null,
                    FlatKeywords: [],
                    HierarchicalKeywords: [],
                    Message: "loc") { Location = new SidecarLocation(null, "Tokyo", null, "Japan") },
                new SidecarImportResult(
                    Success: true,
                    SidecarPath: "orphan.xmp",
                    Rating: 3,
                    ColorLabel: null,
                    FlatKeywords: [],
                    HierarchicalKeywords: [],
                    Message: "orphan"),
            ],
            SuccessCount: 3,
            FailedCount: 0,
            Message: "scan");

        var summary = XmpSidecarImportService.ApplyFolder(
            result,
            findImageForSidecar: s => s switch
            {
                "full.xmp" => "full.jpg",
                "loc.xmp" => "loc.jpg",
                _ => null
            },
            applyRating: (p, r) => ratings.Add((p, r)),
            applyColorLabel: (p, l) => labels.Add((p, l)),
            applyKeywords: (p, k) => keywords.Add((p, k)),
            applyLocation: (p, l) => locations.Add((p, l)));

        Assert.Equal(1, summary.RatingsApplied);
        Assert.Equal(1, summary.LabelsApplied);
        Assert.Equal(1, summary.KeywordsApplied);
        Assert.Equal(1, summary.LocationsApplied);
        Assert.Equal(1, summary.UnmatchedImages);
        Assert.Equal(4, summary.TotalApplied);

        Assert.Equal(("full.jpg", 5), Assert.Single(ratings));
        Assert.Equal(("full.jpg", "Green"), Assert.Single(labels));
        var (kwPath, kwList) = Assert.Single(keywords);
        Assert.Equal("full.jpg", kwPath);
        Assert.Contains("landscape", kwList);
        var (locPath, loc) = Assert.Single(locations);
        Assert.Equal("loc.jpg", locPath);
        Assert.Equal("Tokyo", loc.City);
    }

    // ── Test XMP builders ──────────────────────────────────────────────

    private static string BuildSidecar(
        string? ratingAttr = null,
        string? ratingElement = null,
        string? labelAttr = null,
        IReadOnlyList<string>? dcSubjectItems = null,
        IReadOnlyList<string>? lrHierarchicalItems = null,
        IReadOnlyList<string>? digiKamTagsItems = null,
        string? iptcLocation = null,
        string? city = null,
        string? stateProvince = null,
        string? country = null)
    {
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace lr = "http://ns.adobe.com/lightroom/1.0/";
        XNamespace dk = "http://www.digikam.org/ns/1.0/";
        XNamespace iptc = "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/";
        XNamespace ps = "http://ns.adobe.com/photoshop/1.0/";

        var description = new XElement(rdf + "Description");

        // Rating as attribute
        if (ratingAttr is not null)
            description.SetAttributeValue(xmp + "Rating", ratingAttr);

        // Label as attribute
        if (labelAttr is not null)
            description.SetAttributeValue(xmp + "Label", labelAttr);

        // Location attributes
        if (city is not null)
            description.SetAttributeValue(ps + "City", city);
        if (stateProvince is not null)
            description.SetAttributeValue(ps + "State-Province", stateProvince);
        if (country is not null)
            description.SetAttributeValue(ps + "Country", country);
        if (iptcLocation is not null)
            description.SetAttributeValue(iptc + "Location", iptcLocation);

        // Rating as element
        if (ratingElement is not null)
            description.Add(new XElement(xmp + "Rating", ratingElement));

        // dc:subject bag
        if (dcSubjectItems is { Count: > 0 })
        {
            description.Add(new XElement(dc + "subject",
                new XElement(rdf + "Bag",
                    dcSubjectItems.Select(item => new XElement(rdf + "li", item)))));
        }

        // lr:hierarchicalSubject bag
        if (lrHierarchicalItems is { Count: > 0 })
        {
            description.Add(new XElement(lr + "hierarchicalSubject",
                new XElement(rdf + "Bag",
                    lrHierarchicalItems.Select(item => new XElement(rdf + "li", item)))));
        }

        // digiKam:TagsList bag
        if (digiKamTagsItems is { Count: > 0 })
        {
            description.Add(new XElement(dk + "TagsList",
                new XElement(rdf + "Bag",
                    digiKamTagsItems.Select(item => new XElement(rdf + "li", item)))));
        }

        var doc = new XDocument(
            new XElement(x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xmp", xmp.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "lr", lr.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "digiKam", dk.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "Iptc4xmpCore", iptc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "photoshop", ps.NamespaceName),
                new XElement(rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                    description)));

        return doc.ToString();
    }
}
