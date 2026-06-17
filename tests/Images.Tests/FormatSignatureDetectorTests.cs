using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class FormatSignatureDetectorTests
{
    [Fact]
    public void DetectFromBytes_IdentifiesPng()
    {
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(png);
        Assert.NotNull(result);
        Assert.Equal("PNG", result.FormatName);
        Assert.Equal(".png", result.SuggestedExtension);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesJpeg()
    {
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        var result = FormatSignatureDetector.DetectFromBytes(jpeg);
        Assert.NotNull(result);
        Assert.Equal("JPEG", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesGif89a()
    {
        ReadOnlySpan<byte> gif = "GIF89a"u8;
        var result = FormatSignatureDetector.DetectFromBytes(gif);
        Assert.NotNull(result);
        Assert.Equal("GIF", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesWebP()
    {
        ReadOnlySpan<byte> webp = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
        var result = FormatSignatureDetector.DetectFromBytes(webp);
        Assert.NotNull(result);
        Assert.Equal("WebP", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesBmp()
    {
        ReadOnlySpan<byte> bmp = [0x42, 0x4D, 0x00, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(bmp);
        Assert.NotNull(result);
        Assert.Equal("BMP", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesPdf()
    {
        ReadOnlySpan<byte> pdf = "%PDF-1.7"u8;
        var result = FormatSignatureDetector.DetectFromBytes(pdf);
        Assert.NotNull(result);
        Assert.Equal("PDF", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesPhotoshop()
    {
        ReadOnlySpan<byte> psd = "8BPS"u8;
        var result = FormatSignatureDetector.DetectFromBytes(psd);
        Assert.NotNull(result);
        Assert.Equal("Photoshop", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesQoi()
    {
        ReadOnlySpan<byte> qoi = "qoif"u8;
        var result = FormatSignatureDetector.DetectFromBytes(qoi);
        Assert.NotNull(result);
        Assert.Equal("QOI", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_ReturnsNullForUnknown()
    {
        ReadOnlySpan<byte> unknown = [0x01, 0x02, 0x03, 0x04];
        Assert.Null(FormatSignatureDetector.DetectFromBytes(unknown));
    }

    [Fact]
    public void DetectFromBytes_ReturnsNullForEmpty()
    {
        Assert.Null(FormatSignatureDetector.DetectFromBytes(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void MatchesExtension_ReturnsTrueForAcceptedExtension()
    {
        Assert.True(FormatSignatureDetector.Jpeg.MatchesExtension(".jpg"));
        Assert.True(FormatSignatureDetector.Jpeg.MatchesExtension(".jpeg"));
        Assert.True(FormatSignatureDetector.Jpeg.MatchesExtension(".JFIF"));
    }

    [Fact]
    public void MatchesExtension_ReturnsFalseForWrongExtension()
    {
        Assert.False(FormatSignatureDetector.Jpeg.MatchesExtension(".png"));
        Assert.False(FormatSignatureDetector.Png.MatchesExtension(".jpg"));
    }

    [Fact]
    public void Detect_ReadsFileAndIdentifiesFormat()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

        var result = FormatSignatureDetector.Detect(path);
        Assert.NotNull(result);
        Assert.Equal("PNG", result.FormatName);
    }

    [Fact]
    public void DetectMismatch_ReturnsNullWhenExtensionMatches()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

        Assert.Null(FormatSignatureDetector.DetectMismatch(path));
    }

    [Fact]
    public void DetectMismatch_ReturnsMismatchWhenExtensionIsWrong()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.jpg");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

        var result = FormatSignatureDetector.DetectMismatch(path);
        Assert.NotNull(result);
        Assert.Equal("PNG", result.Value.Signature.FormatName);
        Assert.Equal(".jpg", result.Value.FileExtension);
    }

    [Fact]
    public void HasMismatch_ReturnsTrueForMismatchedFile()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.bmp");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        Assert.True(FormatSignatureDetector.HasMismatch(path));
    }

    [Fact]
    public void HasMismatch_ReturnsFalseForMatchingFile()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        Assert.False(FormatSignatureDetector.HasMismatch(path));
    }

    [Fact]
    public void Detect_ReturnsNullForMissingFile()
    {
        Assert.Null(FormatSignatureDetector.Detect(@"C:\nonexistent\file.png"));
    }

    [Fact]
    public void DetectFromBytes_IdentifiesJxlNakedCodestream()
    {
        ReadOnlySpan<byte> jxl = [0xFF, 0x0A, 0x00, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(jxl);
        Assert.NotNull(result);
        Assert.Equal("JPEG XL", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesIso()
    {
        ReadOnlySpan<byte> ico = [0x00, 0x00, 0x01, 0x00, 0x01, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(ico);
        Assert.NotNull(result);
        Assert.Equal("ICO", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesTiffLittleEndian()
    {
        ReadOnlySpan<byte> tiff = [0x49, 0x49, 0x2A, 0x00, 0x08, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(tiff);
        Assert.NotNull(result);
        Assert.Equal("TIFF", result.FormatName);
    }

    [Fact]
    public void DetectFromBytes_IdentifiesTiffBigEndian()
    {
        ReadOnlySpan<byte> tiff = [0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00];
        var result = FormatSignatureDetector.DetectFromBytes(tiff);
        Assert.NotNull(result);
        Assert.Equal("TIFF", result.FormatName);
    }
}
