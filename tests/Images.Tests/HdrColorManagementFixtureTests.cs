using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class HdrColorManagementFixtureTests
{
    [Fact]
    public void SrgbProfiledJpeg_ReportsIccProfile()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSrgbJpeg(temp.Path, "srgb.jpg");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.Contains(analysis.Rows, r => r.Label == "Profile" && r.Value.Contains("ICC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdobeRgbProfiledTiff_ReportsWideGamutWarning()
    {
        using var temp = TestDirectory.Create();
        var path = WriteAdobeRgbTiff(temp.Path, "adobergb.tiff");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.Contains(analysis.Rows, r => r.Label == "Profile");
        Assert.False(string.IsNullOrEmpty(analysis.WarningText));
    }

    [Fact]
    public void UnprofiledPng_ReportsNoIccProfile()
    {
        using var temp = TestDirectory.Create();
        var path = WriteUnprofiledPng(temp.Path, "noprofile.png");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.Contains(analysis.Rows, r => r.Label == "Profile" && r.Value.Contains("No embedded ICC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SixteenBitTiff_ReportsColorDepth()
    {
        using var temp = TestDirectory.Create();
        var path = Write16BitTiff(temp.Path, "16bit.tiff");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.NotEmpty(analysis.Rows);
    }

    [Fact]
    public void HdrLikeExr_ReportsFloatingPointDepth()
    {
        using var temp = TestDirectory.Create();
        var path = WriteExr(temp.Path, "hdr.exr");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.NotEmpty(analysis.Rows);
    }

    [Fact]
    public void TransparentPngWithProfile_ReportsAlphaAndIcc()
    {
        using var temp = TestDirectory.Create();
        var path = WriteTransparentSrgbPng(temp.Path, "alpha-profiled.png");

        var analysis = ImageColorAnalysisService.Read(path);

        Assert.Contains(analysis.Rows, r => r.Label == "Profile");
        Assert.Contains(analysis.Rows, r => r.Label == "Alpha");
    }

    [Fact]
    public void WpfMagickSufficiency_SrgbRoundtripPreservesProfile()
    {
        using var temp = TestDirectory.Create();
        var source = WriteSrgbJpeg(temp.Path, "roundtrip-source.jpg");
        var dest = Path.Combine(temp.Path, "roundtrip-dest.jpg");

        using (var img = new MagickImage(source))
        {
            Assert.NotNull(img.GetColorProfile());
            img.Write(dest);
        }

        using (var reloaded = new MagickImage(dest))
        {
            var profile = reloaded.GetColorProfile();
            Assert.NotNull(profile);
        }
    }

    private static string WriteSrgbJpeg(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Coral, 16, 16) { Format = MagickFormat.Jpeg };
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }

    private static string WriteAdobeRgbTiff(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.DarkGreen, 16, 16) { Format = MagickFormat.Tiff };
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        image.Write(path);
        return path;
    }

    private static string WriteUnprofiledPng(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Blue, 8, 8) { Format = MagickFormat.Png };
        image.Write(path);
        return path;
    }

    private static string Write16BitTiff(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Yellow, 16, 16) { Format = MagickFormat.Tiff, Depth = 16 };
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }

    private static string WriteExr(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.White, 8, 8) { Format = MagickFormat.Exr };
        image.Write(path);
        return path;
    }

    private static string WriteTransparentSrgbPng(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(255, 0, 0, 128), 8, 8) { Format = MagickFormat.Png };
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }
}
