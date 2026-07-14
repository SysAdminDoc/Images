using Images.Services;

namespace Images.Tests;

public sealed class NativeRuntimeVersionServiceTests
{
    [Theory]
    [InlineData("ImageMagick 7.1.2-27 Q16-HDRI x64", true)]
    [InlineData("7.1.2-2", true)]
    [InlineData("7.1.2-1", false)]
    [InlineData("7.1.1-99", false)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void IsImageMagickVersionSupported_EnforcesReviewedFloor(string? value, bool expected)
        => Assert.Equal(expected, NativeRuntimeVersionService.IsImageMagickVersionSupported(value));

    [Fact]
    public void Current_ReportsLoadedMagickNetImageMagickAndSqliteVersions()
    {
        var versions = NativeRuntimeVersionService.Current;

        Assert.NotEqual("unknown", versions.MagickNetVersion);
        Assert.NotEqual("unknown", versions.ImageMagickVersion);
        Assert.NotEqual("unknown", versions.SqliteVersion);
        Assert.True(versions.ImageMagickSupported, versions.ImageMagickVersion);
        Assert.True(versions.SqliteSupported, versions.SqliteVersion);
    }

    [Fact]
    public void BuildStartupWarnings_ReportsEachUnsupportedNativeRuntime()
    {
        var warnings = NativeRuntimeVersionService.BuildStartupWarnings(
            new NativeRuntimeVersions("14.15.0", "7.1.2-1", "3.49.0"));

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, warning => warning.Contains("ImageMagick", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("SQLite", StringComparison.Ordinal));
    }
}
