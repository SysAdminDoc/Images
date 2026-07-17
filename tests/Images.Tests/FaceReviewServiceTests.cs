using System.IO;
using System.Xml.Linq;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class FaceReviewServiceTests
{
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace MwgRs = "http://www.metadataworkinggroup.com/schemas/regions/";
    private static readonly XNamespace Imv = "https://github.com/SysAdminDoc/Images/ns/1.0/";

    [Fact]
    public void Analyze_ExposesClustersAndQualityWithoutPrivateVectors()
    {
        var detection = new FaceDetection(10, 10, 50, 50, 0.95, []);
        FaceRecognitionResult Analyze(string path) => new(
            true,
            null,
            path,
            "CPU",
            path.EndsWith("a.jpg", StringComparison.Ordinal)
                ? [new FaceEmbedding(path, 0, detection, FaceEmbeddingQuality.Accepted, null, [1, 0])]
                : [new FaceEmbedding(path, 0, detection, FaceEmbeddingQuality.Accepted, null, [0.99f, 0.02f])]);

        var result = FaceReviewService.Analyze(["a.jpg", "b.jpg"], Analyze);

        Assert.Empty(result.Failures);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate => Assert.Equal(1, candidate.ClusterId));
        Assert.DoesNotContain(
            typeof(FaceReviewCandidate).GetProperties(),
            property => property.Name.Contains("Vector", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanMerge_RequiresDecisionForEveryRegionAndNamesForAcceptedRegions()
    {
        var candidates = new[] { Candidate("a.jpg", 0), Candidate("a.jpg", 1) };

        Assert.False(FaceReviewService.CanMerge(candidates,
            [Review("a.jpg", 0, FaceReviewDecision.Accepted, "Ada")], out var missing));
        Assert.Contains("Every", missing, StringComparison.OrdinalIgnoreCase);

        Assert.False(FaceReviewService.CanMerge(candidates,
            [
                Review("a.jpg", 0, FaceReviewDecision.Accepted, " "),
                Review("a.jpg", 1, FaceReviewDecision.Rejected, null),
            ], out var unnamed));
        Assert.Contains("name", unnamed, StringComparison.OrdinalIgnoreCase);

        Assert.True(FaceReviewService.CanMerge(candidates,
            [
                Review("a.jpg", 0, FaceReviewDecision.Accepted, "Ada"),
                Review("a.jpg", 1, FaceReviewDecision.Rejected, null),
            ], out _));
    }

    [Fact]
    public void MergeReviewedRegions_PreservesForeignMetadataAndWritesOnlyAcceptedFaces()
    {
        using var temp = TestDirectory.Create();
        var imagePath = WriteImage(temp.Path, "photo.jpg", 200, 100);
        var sidecarPath = imagePath + ".xmp";
        File.WriteAllText(sidecarPath, $$"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="{{Rdf}}" xmlns:mwg-rs="{{MwgRs}}">
              <rdf:RDF><rdf:Description custom="preserved"><mwg-rs:Regions rdf:parseType="Resource"><mwg-rs:RegionList><rdf:Bag>
                <rdf:li rdf:parseType="Resource"><mwg-rs:Name>Foreign</mwg-rs:Name><mwg-rs:Type>Face</mwg-rs:Type></rdf:li>
              </rdf:Bag></mwg-rs:RegionList></mwg-rs:Regions></rdf:Description></rdf:RDF>
            </x:xmpmeta>
            """);
        var candidates = new[] { Candidate(imagePath, 0), Candidate(imagePath, 1, x: 100) };
        var reviews = new[]
        {
            Review(imagePath, 0, FaceReviewDecision.Accepted, "Ada"),
            Review(imagePath, 1, FaceReviewDecision.Rejected, null),
        };

        var result = FaceReviewService.MergeReviewedRegions(candidates, reviews);

        Assert.True(result.Success);
        var document = XDocument.Load(sidecarPath);
        Assert.Equal("preserved", document.Descendants(Rdf + "Description").Single().Attribute("custom")?.Value);
        var regions = document.Descendants(Rdf + "li").ToArray();
        Assert.Equal(2, regions.Length);
        Assert.Contains(regions, region => region.Element(MwgRs + "Name")?.Value == "Foreign");
        var reviewed = Assert.Single(regions, region => region.Attribute(Imv + "FaceReviewSource")?.Value == "Images");
        Assert.Equal("Ada", reviewed.Element(MwgRs + "Name")?.Value);
        var area = reviewed.Element(MwgRs + "Area")!;
        Assert.Equal("0.175", area.Attribute(XName.Get("x", "http://ns.adobe.com/xmp/sType/Area#"))?.Value);
    }

    [Fact]
    public void MergeReviewedRegions_ReplacesOnlyPriorImagesOwnedRegions()
    {
        using var temp = TestDirectory.Create();
        var imagePath = WriteImage(temp.Path, "photo.jpg", 200, 100);
        var candidates = new[] { Candidate(imagePath, 0) };

        Assert.True(FaceReviewService.MergeReviewedRegions(
            candidates,
            [Review(imagePath, 0, FaceReviewDecision.Accepted, "Old name")]).Success);
        Assert.True(FaceReviewService.MergeReviewedRegions(
            candidates,
            [Review(imagePath, 0, FaceReviewDecision.Accepted, "New name")]).Success);

        var document = XDocument.Load(imagePath + ".xmp");
        var reviewed = document.Descendants(Rdf + "li")
            .Where(region => region.Attribute(Imv + "FaceReviewSource")?.Value == "Images")
            .ToArray();
        Assert.Equal("New name", Assert.Single(reviewed).Element(MwgRs + "Name")?.Value);
    }

    [Fact]
    public void MergeReviewedRegions_DoesNotWriteUntilReviewGatePasses()
    {
        using var temp = TestDirectory.Create();
        var imagePath = WriteImage(temp.Path, "photo.jpg", 200, 100);
        var result = FaceReviewService.MergeReviewedRegions(
            [Candidate(imagePath, 0)],
            [Review(imagePath, 0, FaceReviewDecision.Pending, null)]);

        Assert.False(result.Success);
        Assert.False(File.Exists(imagePath + ".xmp"));
    }

    private static FaceReviewCandidate Candidate(string path, int faceIndex, double x = 10) => new(
        new FaceReviewKey(path, faceIndex),
        new FaceDetection(x, 10, 50, 40, 0.9, []),
        FaceEmbeddingQuality.Accepted,
        null,
        1);

    private static FaceReviewEntry Review(
        string path,
        int faceIndex,
        FaceReviewDecision decision,
        string? name) => new(new FaceReviewKey(path, faceIndex), decision, name);

    private static string WriteImage(string folder, string fileName, uint width, uint height)
    {
        var path = Path.Combine(folder, fileName);
        using var image = new MagickImage(MagickColors.SkyBlue, width, height);
        image.Write(path, MagickFormat.Jpeg);
        return path;
    }
}
