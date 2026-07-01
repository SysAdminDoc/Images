using System.IO;
using System.IO.Compression;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class VectorFidelityFixtureTests
{
    [Fact]
    public void Svg_SimpleFill_DecodesToRasterWithMagick()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "simple.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect fill="red" width="100" height="100"/></svg>""");

        using var img = new MagickImage(path);
        Assert.True(img.Width > 0);
        Assert.True(img.Height > 0);
    }

    [Fact]
    public void Svg_TransparentBackground_PreservesAlpha()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "transparent.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><circle cx="32" cy="32" r="16" fill="blue"/></svg>""");

        using var img = new MagickImage(path);
        Assert.True(img.HasAlpha);
    }

    [Fact]
    public void Svg_MagickDensityHint_DoesNotScaleOutput()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "scale.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" width="50" height="50"><rect fill="green" width="50" height="50"/></svg>""");

        using var imgLow = new MagickImage();
        imgLow.Density = new Density(72);
        imgLow.Read(path);

        using var imgHigh = new MagickImage();
        imgHigh.Density = new Density(300);
        imgHigh.Read(path);

        Assert.Equal(imgLow.Width, imgHigh.Width);
    }

    [Fact]
    public void Svg_EmbeddedRasterImage_DoesNotThrow()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "embedded.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="64" height="64"><image href="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==" width="64" height="64"/></svg>""");

        using var img = new MagickImage(path);
        Assert.True(img.Width > 0);
    }

    [Fact]
    public void Svg_TextElement_DecodesWithoutCrash()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "text.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" width="200" height="50"><text x="10" y="30" font-size="20" fill="white">Hello</text></svg>""");

        using var img = new MagickImage(path);
        Assert.True(img.Width > 0);
    }

    [Fact]
    public void Svgz_GzipCompressed_DecodesCorrectly()
    {
        using var temp = TestDirectory.Create();
        var svgContent = """<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><rect fill="blue" width="32" height="32"/></svg>""";
        var path = Path.Combine(temp.Path, "compressed.svgz");

        using (var fs = File.Create(path))
        using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gz))
        {
            writer.Write(svgContent);
        }

        using var img = new MagickImage(path);
        Assert.True(img.Width > 0);
    }

    [Fact]
    public void Svg_IsInSupportedExtensions()
    {
        Assert.Contains(".svg", SupportedImageFormats.Extensions);
        Assert.Contains(".svgz", SupportedImageFormats.Extensions);
    }

    [Fact]
    public void Svg_AnimatedSmil_DegradesToStaticFrame()
    {
        using var temp = TestDirectory.Create();
        var path = WriteSvg(temp.Path, "animated.svg",
            """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect fill="red" width="100" height="100"><animate attributeName="fill" values="red;blue;red" dur="2s" repeatCount="indefinite"/></rect></svg>""");

        using var img = new MagickImage(path);
        Assert.True(img.Width > 0);
    }

    private static string WriteSvg(string folder, string name, string content)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }
}
