using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public void Rebuild_IndexesImageMetadataFingerprintAndSidecarState()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        File.WriteAllText(
            source + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:RDF>
                <rdf:Description xmp:Rating="4">
                  <dc:subject>
                    <rdf:Bag>
                      <rdf:li>project:Images</rdf:li>
                      <rdf:li>Needs Review</rdf:li>
                    </rdf:Bag>
                  </dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        var asset = Assert.Single(result.Assets);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(Path.GetFullPath(source), asset.SourcePath);
        Assert.Equal(64, asset.Fingerprint.Length);
        Assert.Equal(16, asset.Width);
        Assert.Equal(8, asset.Height);
        Assert.Equal("PNG", asset.Format);
        Assert.Equal(4, asset.Rating);
        Assert.Contains("project:images", asset.Tags);
        Assert.Contains("needs-review", asset.Tags);
        Assert.Equal(source + ".xmp", asset.SidecarPath);

        var stored = service.GetByPath(source);
        Assert.NotNull(stored);
        Assert.Equal(asset.Fingerprint, stored.Fingerprint);
        Assert.Equal(asset.Tags, stored.Tags);
    }

    [Fact]
    public void Rebuild_ClearsPreviousRowsBecauseCatalogIsRebuildableCache()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 8);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([temp.Path]);
        File.Delete(source);

        var result = service.Rebuild([temp.Path]);

        Assert.Empty(result.Assets);
        Assert.Empty(service.GetAllAssets());
    }

    [Fact]
    public void Rebuild_SkipsUnsupportedFiles()
    {
        using var temp = TestDirectory.Create();
        temp.WriteFile("notes.txt", "not an image");
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        Assert.Empty(result.Assets);
        Assert.Equal(0, result.FailedCount);
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
