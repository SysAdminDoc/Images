using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class MetadataEditServiceTests
{
    [Fact]
    public void StripMetadata_GpsCategory_RemovesGpsTags()
    {
        using var image = CreateImageWithExif();

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.Gps);

        Assert.True(result.RemovedCount > 0);
        Assert.All(result.RemovedTagNames, name =>
            Assert.StartsWith("Gps", name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StripMetadata_DeviceInfo_RemovesDeviceTags()
    {
        using var image = CreateImageWithExif();

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.DeviceInfo);

        Assert.True(result.RemovedCount > 0);
        Assert.Contains(result.RemovedTagNames, n => n == "Make" || n == "Model");
    }

    [Fact]
    public void StripMetadata_None_RemovesNothing()
    {
        using var image = CreateImageWithExif();

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.None);

        Assert.Equal(0, result.RemovedCount);
        Assert.Empty(result.RemovedTagNames);
    }

    [Fact]
    public void StripMetadata_All_RemovesAllRecognizedCategories()
    {
        using var image = CreateImageWithExif();

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.All);

        Assert.True(result.RemovedCount > 0);
    }

    [Fact]
    public void StripMetadata_FilePath_WritesBackInPlace()
    {
        using var temp = TestDirectory.Create();
        var path = WriteImageWithExif(temp.Path, "tagged.jpg");

        var result = MetadataEditService.StripMetadata(path, MetadataStripCategory.Gps);

        Assert.True(result.RemovedCount > 0);
        Assert.True(File.Exists(path));

        // Verify GPS tags are actually gone from the file
        using var reloaded = new MagickImage(path);
        var exif = reloaded.GetExifProfile();
        if (exif is not null)
        {
            Assert.DoesNotContain(exif.Values, v =>
                v.Tag.ToString().StartsWith("Gps", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static MagickImage CreateImageWithExif()
    {
        var image = new MagickImage(MagickColors.Blue, 8, 8) { Format = MagickFormat.Jpeg };
        var exif = new ExifProfile();
        exif.SetValue(ImageMagick.ExifTag.Make, "TestCamera");
        exif.SetValue(ImageMagick.ExifTag.Model, "TestModel");
        exif.SetValue(ImageMagick.ExifTag.Software, "TestSoftware");
        exif.SetValue(ImageMagick.ExifTag.GPSLatitude, new Rational[] { new(47, 1), new(36, 1), new(0, 1) });
        exif.SetValue(ImageMagick.ExifTag.GPSLongitude, new Rational[] { new(122, 1), new(19, 1), new(0, 1) });
        exif.SetValue(ImageMagick.ExifTag.DateTime, "2024:01:15 10:30:00");
        image.SetProfile(exif);
        return image;
    }

    private static string WriteImageWithExif(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = CreateImageWithExif();
        image.Write(path);
        return path;
    }
}
