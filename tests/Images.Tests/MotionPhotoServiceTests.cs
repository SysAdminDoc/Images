using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class MotionPhotoServiceTests
{
    [Fact]
    public void IsMotionPhotoCandidate_TrueForJpeg()
    {
        Assert.True(MotionPhotoService.IsMotionPhotoCandidate("photo.jpg"));
        Assert.True(MotionPhotoService.IsMotionPhotoCandidate("photo.jpeg"));
        Assert.True(MotionPhotoService.IsMotionPhotoCandidate("photo.HEIC"));
    }

    [Fact]
    public void IsMotionPhotoCandidate_FalseForNonJpeg()
    {
        Assert.False(MotionPhotoService.IsMotionPhotoCandidate("photo.png"));
        Assert.False(MotionPhotoService.IsMotionPhotoCandidate("photo.webp"));
        Assert.False(MotionPhotoService.IsMotionPhotoCandidate("photo.gif"));
    }

    [Fact]
    public void Detect_ReturnsNullForNonJpeg()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Assert.Null(MotionPhotoService.Detect(path));
    }

    [Fact]
    public void Detect_ReturnsNullForPlainJpeg()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "test.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0xFF, 0xD9]);
        Assert.Null(MotionPhotoService.Detect(path));
    }

    [Fact]
    public void Detect_FindsEmbeddedMp4()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "motion.jpg");

        // Simulate: JPEG data followed by an MP4 ftyp box
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        // Add some padding to make it realistic
        var padding = new byte[100];
        // ftyp box: size (20 bytes) + "ftyp" + "isom" brand
        var ftypBox = new byte[]
        {
            0x00, 0x00, 0x00, 0x14, // box size = 20
            0x66, 0x74, 0x79, 0x70, // "ftyp"
            0x69, 0x73, 0x6F, 0x6D, // "isom" brand
            0x00, 0x00, 0x02, 0x00, // minor version
            0x69, 0x73, 0x6F, 0x6D  // compatible brand
        };
        var mp4Data = new byte[50]; // simulated mp4 payload

        using (var stream = File.Create(path))
        {
            stream.Write(jpegData);
            stream.Write(padding);
            stream.Write(ftypBox);
            stream.Write(mp4Data);
        }

        var result = MotionPhotoService.Detect(path);
        Assert.NotNull(result);
        Assert.Equal("isom", result.ContainerType);
        Assert.True(result.VideoOffset > 0);
        Assert.True(result.VideoLength > 0);
    }

    [Fact]
    public void FindCompanionVideo_ReturnsNullWhenNoCompanion()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "photo.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8]);
        Assert.Null(MotionPhotoService.FindCompanionVideo(path));
    }

    [Fact]
    public void FindCompanionVideo_FindsMovFile()
    {
        using var temp = TestDirectory.Create();
        var jpgPath = Path.Combine(temp.Path, "photo.jpg");
        var movPath = Path.Combine(temp.Path, "photo.mov");
        File.WriteAllBytes(jpgPath, [0xFF, 0xD8]);
        File.WriteAllBytes(movPath, [0x00]);

        var result = MotionPhotoService.FindCompanionVideo(jpgPath);
        Assert.NotNull(result);
        Assert.EndsWith(".mov", result);
    }

    [Fact]
    public void FindCompanionVideo_FindsMp4File()
    {
        using var temp = TestDirectory.Create();
        var jpgPath = Path.Combine(temp.Path, "photo.jpg");
        var mp4Path = Path.Combine(temp.Path, "photo.mp4");
        File.WriteAllBytes(jpgPath, [0xFF, 0xD8]);
        File.WriteAllBytes(mp4Path, [0x00]);

        var result = MotionPhotoService.FindCompanionVideo(jpgPath);
        Assert.NotNull(result);
        Assert.EndsWith(".mp4", result);
    }

    [Fact]
    public void ExtractEmbeddedVideo_WritesVideoFile()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "motion.jpg");

        var jpegPart = new byte[100];
        jpegPart[0] = 0xFF;
        jpegPart[1] = 0xD8;
        var videoPart = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };

        using (var stream = File.Create(path))
        {
            stream.Write(jpegPart);
            stream.Write(videoPart);
        }

        var info = new MotionPhotoInfo(100, 8, "isom");
        var outputPath = MotionPhotoService.ExtractEmbeddedVideo(path, info, temp.Path);

        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));
        var extracted = File.ReadAllBytes(outputPath);
        Assert.Equal(videoPart, extracted);
    }

    [Fact]
    public void ExtractEmbeddedVideo_ReturnsNullForMissingFile()
    {
        var info = new MotionPhotoInfo(100, 8, "isom");
        Assert.Null(MotionPhotoService.ExtractEmbeddedVideo(@"C:\nonexistent.jpg", info));
    }

    [Fact]
    public void Detect_ReturnsNullForMissingFile()
    {
        Assert.Null(MotionPhotoService.Detect(@"C:\nonexistent\photo.jpg"));
    }
}
