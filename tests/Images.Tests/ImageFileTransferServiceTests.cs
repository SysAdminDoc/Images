using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ImageFileTransferServiceTests
{
    [Fact]
    public void Copy_ResolvesNameCollisionAndCopiesMatchingSidecars()
    {
        using var temp = TestDirectory.Create();
        var sourceFolder = Path.Combine(temp.Path, "source");
        var destinationFolder = Path.Combine(temp.Path, "destination");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(destinationFolder);

        var source = Path.Combine(sourceFolder, "photo.jpg");
        File.WriteAllText(source, "image");
        File.WriteAllText(source + ".xmp", "primary-sidecar");
        File.WriteAllText(Path.Combine(sourceFolder, "photo.xmp"), "stem-sidecar");
        File.WriteAllText(Path.Combine(destinationFolder, "photo.jpg"), "existing");

        var result = new ImageFileTransferService().Transfer(source, destinationFolder, ImageFileTransferMode.Copy);

        var expectedImage = Path.Combine(destinationFolder, "photo (2).jpg");
        Assert.Equal(ImageFileTransferStatus.Succeeded, result.Status);
        Assert.Equal(expectedImage, result.DestinationPath);
        Assert.True(File.Exists(source));
        Assert.Equal("image", File.ReadAllText(expectedImage));
        Assert.Equal("primary-sidecar", File.ReadAllText(expectedImage + ".xmp"));
        Assert.Equal("stem-sidecar", File.ReadAllText(Path.Combine(destinationFolder, "photo (2).xmp")));
        Assert.Equal(
            [expectedImage + ".xmp", Path.Combine(destinationFolder, "photo (2).xmp")],
            result.SidecarDestinationPaths);
    }

    [Fact]
    public void Move_MovesImageAndMatchingSidecars()
    {
        using var temp = TestDirectory.Create();
        var sourceFolder = Path.Combine(temp.Path, "source");
        var destinationFolder = Path.Combine(temp.Path, "destination");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(destinationFolder);

        var source = Path.Combine(sourceFolder, "scan.png");
        var sourcePrimarySidecar = source + ".xmp";
        File.WriteAllText(source, "image");
        File.WriteAllText(sourcePrimarySidecar, "primary-sidecar");

        var result = new ImageFileTransferService().Transfer(source, destinationFolder, ImageFileTransferMode.Move);

        var expectedImage = Path.Combine(destinationFolder, "scan.png");
        Assert.Equal(ImageFileTransferStatus.Succeeded, result.Status);
        Assert.Equal(expectedImage, result.DestinationPath);
        Assert.False(File.Exists(source));
        Assert.False(File.Exists(sourcePrimarySidecar));
        Assert.Equal("image", File.ReadAllText(expectedImage));
        Assert.Equal("primary-sidecar", File.ReadAllText(expectedImage + ".xmp"));
    }

    [Fact]
    public void Move_ToCurrentFolderReturnsAlreadyInDestination()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "scan.png");
        File.WriteAllText(source, "image");

        var result = new ImageFileTransferService().Transfer(source, temp.Path, ImageFileTransferMode.Move);

        Assert.Equal(ImageFileTransferStatus.AlreadyInDestination, result.Status);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Transfer_WhenDestinationFolderIsMissingReturnsDestinationMissing()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "scan.png");
        File.WriteAllText(source, "image");

        var result = new ImageFileTransferService().Transfer(
            source,
            Path.Combine(temp.Path, "missing"),
            ImageFileTransferMode.Copy);

        Assert.Equal(ImageFileTransferStatus.DestinationMissing, result.Status);
        Assert.True(File.Exists(source));
    }
}
