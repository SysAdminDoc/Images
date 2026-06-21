using Images.Services;

namespace Images.Tests;

public sealed class ImageMetadataServiceTests
{
    [Fact]
    public void Read_NullPath_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(null!);
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_EmptyPath_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(string.Empty);
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_NonexistentFile_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(@"C:\__nonexistent_test_path__\image.jpg");
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_GhostscriptFormat_ReturnsEmpty()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("test.pdf", "dummy");
        var result = ImageMetadataService.Read(path);
        Assert.Equal(PhotoMetadata.Empty, result);
    }
}
