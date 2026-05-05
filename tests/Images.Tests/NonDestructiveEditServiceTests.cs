using System.IO;
using System.Xml.Linq;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class NonDestructiveEditServiceTests
{
    [Fact]
    public void AppendOperation_WritesJsonEditStackInsideExistingXmpSidecar()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        File.WriteAllText(
            source + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:dc="http://purl.org/dc/elements/1.1/">
                <rdf:Description>
                  <dc:subject>
                    <rdf:Bag><rdf:li>keep-this-tag</rdf:li></rdf:Bag>
                  </dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """);

        var service = new NonDestructiveEditService();
        var result = service.AppendOperation(
            source,
            "crop",
            new Dictionary<string, string>
            {
                ["x"] = "1",
                ["y"] = "1",
                ["width"] = "4",
                ["height"] = "2"
            },
            "Test crop");

        Assert.True(result.Success);
        var snapshot = service.LoadSnapshot(source);
        var operation = Assert.Single(snapshot.Operations);
        Assert.Equal("crop", operation.Kind);
        Assert.Equal("4", operation.Parameters["width"]);

        var document = XDocument.Load(source + ".xmp");
        Assert.Contains(
            document.Descendants().Select(element => element.Value),
            value => value.Contains("keep-this-tag", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            document.Descendants().Select(element => element.Name.LocalName),
            name => name.Equals("EditStack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Export_AppliesEnabledOperationsAndWritesProvenanceSidecar()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        var target = Path.Combine(temp.Path, "out", "export.jpg");
        var service = new NonDestructiveEditService();
        service.AppendOperation(
            source,
            "crop",
            new Dictionary<string, string>
            {
                ["x"] = "2",
                ["y"] = "1",
                ["width"] = "4",
                ["height"] = "2"
            });

        var result = service.Export(source, target);

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedOperationCount);
        Assert.True(File.Exists(result.OutputPath));
        Assert.True(File.Exists(result.ProvenanceSidecarPath));

        using var exported = new MagickImage(result.OutputPath);
        Assert.Equal((uint)4, exported.Width);
        Assert.Equal((uint)2, exported.Height);

        var provenance = XDocument.Load(result.ProvenanceSidecarPath).ToString();
        Assert.Contains("ExportProvenance", provenance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("crop", provenance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SourceSha256", provenance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetOperationEnabled_DisabledOperationsDoNotApplyOnExport()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        var target = Path.Combine(temp.Path, "export.png");
        var service = new NonDestructiveEditService();
        var mutation = service.AppendOperation(
            source,
            "crop",
            new Dictionary<string, string>
            {
                ["x"] = "0",
                ["y"] = "0",
                ["width"] = "4",
                ["height"] = "2"
            });

        Assert.NotNull(mutation.Operation);
        service.SetOperationEnabled(source, mutation.Operation!.Id, enabled: false);

        var result = service.Export(source, target);

        Assert.True(result.Success);
        Assert.Equal(0, result.AppliedOperationCount);
        using var exported = new MagickImage(result.OutputPath);
        Assert.Equal((uint)8, exported.Width);
        Assert.Equal((uint)4, exported.Height);
    }

    [Fact]
    public void Export_AppliesAdjustmentOperation()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        var baseline = ImageExportService.Save(
            source,
            Path.Combine(temp.Path, "baseline.png"),
            []);

        var service = new NonDestructiveEditService();
        var mutation = service.AppendOperation(
            source,
            "adjust",
            new ImageAdjustmentPlan(8, 94, 1.1, 18, 24, 120, 84).ToEditParameters());

        Assert.True(mutation.Success);

        var result = service.Export(source, Path.Combine(temp.Path, "adjusted.png"));

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedOperationCount);
        Assert.False(File.ReadAllBytes(baseline).SequenceEqual(File.ReadAllBytes(result.OutputPath)));

        var provenance = XDocument.Load(result.ProvenanceSidecarPath).ToString();
        Assert.Contains("adjust", provenance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_AppliesLocalExposureOperation()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        var baseline = ImageExportService.Save(
            source,
            Path.Combine(temp.Path, "baseline.png"),
            []);

        var service = new NonDestructiveEditService();
        var mutation = service.AppendOperation(
            source,
            "local-exposure",
            LocalExposureBrushService.ToEditParameters(
                new[] { new LocalExposureBrushStroke(4, 2, 4, 0.65) }));

        Assert.True(mutation.Success);

        var result = service.Export(source, Path.Combine(temp.Path, "dodged.png"));

        Assert.True(result.Success);
        Assert.Equal(1, result.AppliedOperationCount);
        Assert.False(File.ReadAllBytes(baseline).SequenceEqual(File.ReadAllBytes(result.OutputPath)));

        var provenance = XDocument.Load(result.ProvenanceSidecarPath).ToString();
        Assert.Contains("local-exposure", provenance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateVirtualCopy_ForksOperationsWithoutDuplicatingPixels()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 4);
        var service = new NonDestructiveEditService();
        service.AppendOperation(
            source,
            "resize",
            new Dictionary<string, string>
            {
                ["width"] = "4",
                ["height"] = "2"
            });

        var copy = service.CreateVirtualCopy(source, "Small proof");

        Assert.True(copy.Success);
        Assert.NotNull(copy.VirtualCopy);
        var snapshot = service.LoadSnapshot(source);
        var virtualCopy = Assert.Single(snapshot.VirtualCopies);
        Assert.Equal("Small proof", virtualCopy.Name);
        Assert.Single(virtualCopy.Operations);
        Assert.True(File.Exists(source));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "*.png"));
    }

    private static string WriteImage(string folder, string name, uint width, uint height)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, width, height)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }
}
