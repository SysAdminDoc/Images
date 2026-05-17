using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageColorAnalysisServiceTests
{
    [Fact]
    public void Read_ProfiledImageReportsIccAndHistogramRows()
    {
        using var temp = TestDirectory.Create();
        var source = WriteProfiledImage(temp.Path, "profiled.jpg");

        var analysis = ImageColorAnalysisService.Read(source);

        Assert.Contains(analysis.Rows, row => row.Label == "Profile" && row.Value.Contains("ICC", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.Rows, row => row.Label == "Red" && row.Value.Contains("mean", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.Rows, row => row.Label == "Histogram" && row.Value.Contains("shadows", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("does not change pixels", analysis.WarningText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_UnprofiledImageReportsUnmanagedWarning()
    {
        using var temp = TestDirectory.Create();
        var source = WriteUnprofiledImage(temp.Path, "plain.png");

        var analysis = ImageColorAnalysisService.Read(source);

        Assert.Contains(analysis.Rows, row => row.Label == "Profile" && row.Value.Contains("No embedded ICC", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("No embedded ICC", analysis.WarningText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_TransparentImageReportsAlphaStats()
    {
        using var temp = TestDirectory.Create();
        var source = WriteTransparentImage(temp.Path, "transparent.png");

        var analysis = ImageColorAnalysisService.Read(source);

        Assert.Contains(analysis.Rows, row => row.Label == "Alpha" && row.Value.Contains("transparent", StringComparison.OrdinalIgnoreCase));
    }

    private static string WriteProfiledImage(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, 8, 8)
        {
            Format = MagickFormat.Jpeg
        };
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }

    private static string WriteUnprofiledImage(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Blue, 8, 8)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }

    private static string WriteTransparentImage(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Transparent, 8, 8)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }
}
