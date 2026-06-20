using Images.Services;

namespace Images.Tests;

public sealed class SupportedImageFormatsTests
{
    [Theory]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.png", true)]
    [InlineData("photo.webp", true)]
    [InlineData("photo.heic", true)]
    [InlineData("photo.cr2", true)]
    [InlineData("photo.txt", false)]
    [InlineData("photo.mp4", false)]
    public void IsSupported_ClassifiesExtensionCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, SupportedImageFormats.IsSupported(path));
    }

    [Theory]
    [InlineData("archive.zip", true)]
    [InlineData("archive.cbz", true)]
    [InlineData("archive.rar", true)]
    [InlineData("archive.cbr", true)]
    [InlineData("archive.7z", true)]
    [InlineData("archive.cb7", true)]
    [InlineData("photo.png", false)]
    public void IsArchive_ClassifiesArchiveExtensions(string path, bool expected)
    {
        Assert.Equal(expected, SupportedImageFormats.IsArchive(path));
    }

    [Theory]
    [InlineData("doc.pdf", true)]
    [InlineData("doc.ps", true)]
    [InlineData("doc.eps", true)]
    [InlineData("doc.ai", true)]
    [InlineData("photo.png", false)]
    public void RequiresGhostscript_ClassifiesDocumentFormats(string path, bool expected)
    {
        Assert.Equal(expected, SupportedImageFormats.RequiresGhostscript(path));
    }

    [Theory]
    [InlineData(".jpg", "Common images")]
    [InlineData(".psd", "Design and production")]
    [InlineData(".cr2", "Camera RAW")]
    [InlineData(".zip", "Archive books")]
    [InlineData(".svg", "Vector previews")]
    [InlineData(".pdf", "Document previews")]
    [InlineData(".pbm", "Portable and scientific")]
    [InlineData(".mp4", null)]
    public void FamilyLabelForExtension_ReturnsCorrectFamily(string extension, string? expected)
    {
        Assert.Equal(expected, SupportedImageFormats.FamilyLabelForExtension(extension));
    }

    [Theory]
    [InlineData("JPG", true)]
    [InlineData("png", true)]
    [InlineData("Webp", true)]
    public void IsSupportedExtension_IsCaseInsensitive(string extension, bool expected)
    {
        Assert.Equal(expected, SupportedImageFormats.IsSupportedExtension(extension));
    }
}
