using System.IO;
using ImageMagick;
using Images.Services;
using System.Text.Json;

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
    public void TryParse_RootLifecycleCommands_RequireExactArguments()
    {
        Assert.True(CatalogCli.TryParse(["--catalog-root-add", @"C:\Photos"], out var add, out var error));
        Assert.Null(error);
        Assert.Equal(CatalogCliMode.RootAdd, add!.Mode);
        Assert.Equal(@"C:\Photos", add.RootPath);

        Assert.True(CatalogCli.TryParse(["--catalog-root-list"], out var list, out error));
        Assert.Null(error);
        Assert.Equal(CatalogCliMode.RootList, list!.Mode);

        Assert.True(CatalogCli.TryParse(["--catalog-rescan", "extra"], out var invalid, out error));
        Assert.Null(invalid);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void TryParse_Stacks_AcceptsDefaultsOrValidatedThresholds()
    {
        Assert.True(CatalogCli.TryParse(["--catalog-stacks"], out var defaults, out var error));
        Assert.Null(error);
        Assert.Equal(CatalogCliMode.Stacks, defaults!.Mode);
        Assert.Equal(6, defaults.MaxHashDistance);

        Assert.True(CatalogCli.TryParse(["--catalog-stacks", "4", "30", "100"], out var custom, out error));
        Assert.Null(error);
        Assert.Equal(4, custom!.MaxHashDistance);
        Assert.Equal(30, custom.MaxCaptureSeconds);
        Assert.Equal(100, custom.MaxGeoDistanceMeters);

        Assert.True(CatalogCli.TryParse(["--catalog-stacks", "65", "30", "100"], out var invalid, out error));
        Assert.Null(invalid);
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

    [Fact]
    public void Execute_RootLifecycle_AddsListsRescansAndRemovesWithoutWindows()
    {
        using var temp = TestDirectory.Create();
        var image = WriteImage(temp.Path, "watched.png");
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, CatalogCli.Execute(new CatalogCliRequest(CatalogCliMode.RootAdd, RootPath: temp.Path), output, error, catalog));
        Assert.NotNull(catalog.GetByPath(image));

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Assert.Equal(0, CatalogCli.Execute(new CatalogCliRequest(CatalogCliMode.RootList), output, error, catalog));
        Assert.Equal(temp.Path, output.ToString().Trim());

        Assert.Equal(0, CatalogCli.Execute(new CatalogCliRequest(CatalogCliMode.Rescan), output, error, catalog));
        Assert.Equal(0, CatalogCli.Execute(new CatalogCliRequest(CatalogCliMode.RootRemove, RootPath: temp.Path), output, error, catalog));
        Assert.Empty(catalog.GetRoots());
        Assert.Null(catalog.GetByPath(image));
    }

    [Fact]
    public void Execute_Stacks_PrintsOneJsonObjectPerNearDuplicateGroup()
    {
        using var temp = TestDirectory.Create();
        var first = WriteImage(temp.Path, "burst-a.png", MagickColors.Red);
        var second = WriteImage(temp.Path, "burst-b.png", MagickColors.Blue);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([temp.Path]);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CatalogCli.Execute(
            new CatalogCliRequest(CatalogCliMode.Stacks),
            output,
            error,
            catalog);

        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(output.ToString().Trim());
        var assets = document.RootElement.GetProperty("assets").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(first, assets);
        Assert.Contains(second, assets);
        Assert.Contains("Found 1 near-duplicate stacks.", error.ToString());
    }

    private static string WriteImage(string folder, string name)
        => WriteImage(folder, name, MagickColors.Red);

    private static string WriteImage(string folder, string name, IMagickColor<float> color)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(color, 4, 4) { Format = MagickFormat.Png };
        image.Write(path);
        return path;
    }
}
