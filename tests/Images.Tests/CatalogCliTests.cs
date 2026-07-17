using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class CatalogCliTests
{
    [Fact]
    public void TryParse_Search_RequiresExactTermsArgument()
    {
        Assert.True(CatalogCli.TryParse(["--catalog-search", "sunset beach"], out var request, out var error));
        Assert.Null(error);
        Assert.Equal(CatalogCliMode.Search, request!.Mode);
        Assert.Equal("sunset beach", request.SearchTerms);

        Assert.True(CatalogCli.TryParse(["--catalog-search"], out request, out error));
        Assert.Null(request);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void TryParse_Near_UsesInvariantCoordinatesAndValidatesRanges()
    {
        Assert.True(CatalogCli.TryParse(["--catalog-near", "48.8583", "2.2945", "5"], out var request, out var error));
        Assert.Null(error);
        Assert.Equal(CatalogCliMode.Near, request!.Mode);
        Assert.Equal(5, request.RadiusKm);

        Assert.True(CatalogCli.TryParse(["--catalog-near", "91", "2", "5"], out request, out error));
        Assert.Null(request);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void Execute_Search_PrintsOneMatchingPathPerLine()
    {
        using var temp = TestDirectory.Create();
        var expected = WriteImage(temp.Path, "sunset.png");
        WriteImage(temp.Path, "portrait.png");
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([temp.Path]);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CatalogCli.Execute(
            new CatalogCliRequest(CatalogCliMode.Search, SearchTerms: "sunset"),
            output,
            error,
            catalog);

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, output.ToString().Trim());
        Assert.Contains("Matched 1 catalog assets.", error.ToString());
    }

    private static string WriteImage(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, 4, 4) { Format = MagickFormat.Png };
        image.Write(path);
        return path;
    }
}
