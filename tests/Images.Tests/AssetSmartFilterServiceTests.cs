using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class AssetSmartFilterServiceTests
{
    [Fact]
    public void Filter_MatchesFormatOrientationDimensionsDatePaletteAndDuplicateStatus()
    {
        using var temp = TestDirectory.Create();
        var now = new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero);
        var landscape = WriteImage(temp.Path, "landscape.png", MagickColors.Red, 4200, 3000, now.AddDays(-2));
        var duplicate = Path.Combine(temp.Path, "landscape-copy.png");
        File.Copy(landscape, duplicate);
        File.SetLastWriteTimeUtc(duplicate, now.AddDays(-2).UtcDateTime);
        var portrait = WriteImage(temp.Path, "portrait.jpg", MagickColors.Blue, 900, 1800, now.AddDays(-20));

        var result = AssetSmartFilterService.Filter(
            AssetSmartFilterService.BuildIndex([landscape, duplicate, portrait], now),
            "format:png orientation:landscape size:large date:week palette:red duplicate:yes");

        Assert.Equal(
            new[] { landscape, duplicate }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            result.Items.Select(item => item.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        Assert.True(result.HasStructuredFilters);
        Assert.Contains("format:png", result.Summary);
        Assert.Contains("duplicates", result.Summary);
    }

    [Fact]
    public void Filter_ReadsRatingAndKeywordSidecars()
    {
        using var temp = TestDirectory.Create();
        var now = new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero);
        var portrait = WriteImage(temp.Path, "portrait.png", MagickColors.Blue, 900, 1800, now.AddDays(-20));
        var landscape = WriteImage(temp.Path, "landscape.png", MagickColors.Green, 1800, 900, now.AddDays(-20));
        File.WriteAllText(
            portrait + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                       xmlns:xmp="http://ns.adobe.com/xap/1.0/"
                       xmlns:dc="http://purl.org/dc/elements/1.1/">
                <rdf:Description xmp:Rating="5">
                  <dc:subject>
                    <rdf:Bag>
                      <rdf:li>Portrait</rdf:li>
                      <rdf:li>Client Select</rdf:li>
                    </rdf:Bag>
                  </dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """);

        var result = AssetSmartFilterService.Filter(
            AssetSmartFilterService.BuildIndex([portrait, landscape], now),
            "rating:>=4 tag:portrait palette:blue orientation:portrait date:month");

        var item = Assert.Single(result.Items);
        Assert.Equal(portrait, item.Path);
        Assert.Equal(5, item.Rating);
        Assert.Contains("Portrait", item.Tags);
    }

    [Fact]
    public void Filter_SupportsFolderTextUniqueAndUnratedQueries()
    {
        using var temp = TestDirectory.Create();
        var now = new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero);
        var unique = WriteImage(temp.Path, "unique.png", MagickColors.Gray, 32, 32, now.AddDays(-400));
        var duplicateA = WriteImage(temp.Path, "dupe-a.png", MagickColors.Yellow, 32, 32, now);
        var duplicateB = Path.Combine(temp.Path, "dupe-b.png");
        File.Copy(duplicateA, duplicateB);
        File.SetLastWriteTimeUtc(duplicateB, now.UtcDateTime);

        var result = AssetSmartFilterService.Filter(
            AssetSmartFilterService.BuildIndex([unique, duplicateA, duplicateB], now),
            $"folder:{Path.GetFileName(temp.Path)} duplicate:no rating:unrated date:older gray");

        var item = Assert.Single(result.Items);
        Assert.Equal(unique, item.Path);
        Assert.Equal("unique", item.DuplicateStatus);
        Assert.Equal("gray", item.Palette);
    }

    private static string WriteImage(
        string folder,
        string fileName,
        IMagickColor<ushort> color,
        uint width,
        uint height,
        DateTimeOffset modified)
    {
        var path = Path.Combine(folder, fileName);
        using var image = new MagickImage(color, width, height)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        File.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        return path;
    }
}
