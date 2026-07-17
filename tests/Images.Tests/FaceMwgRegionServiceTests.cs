using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class FaceMwgRegionServiceTests
{
    [Fact]
    public void BuildDraft_WritesNormalizedMwgFaceAreaWithoutSourcePath()
    {
        var result = new FaceDetectionResult(
            FaceDetectionStatus.Success,
            null,
            @"C:\private\portrait.jpg",
            100,
            80,
            "CPU",
            [new FaceDetection(20, 10, 40, 20, 0.95, [])]);

        var document = FaceMwgRegionService.BuildDraft(result);
        var xml = document.ToString(SaveOptions.DisableFormatting);
        var area = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Area");

        Assert.Equal("Face", Assert.Single(document.Descendants(), element => element.Name.LocalName == "Type").Value);
        Assert.Equal("0.4", area.Attributes().Single(attribute => attribute.Name.LocalName == "x").Value);
        Assert.Equal("0.25", area.Attributes().Single(attribute => attribute.Name.LocalName == "y").Value);
        Assert.DoesNotContain("private", xml, StringComparison.OrdinalIgnoreCase);
    }
}
