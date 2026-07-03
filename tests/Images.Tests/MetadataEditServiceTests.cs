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

    [Fact]
    public void StripMetadata_GpsCategory_RemovesXmpLocationButKeepsRating()
    {
        using var image = CreateImageWithExif();
        image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes(XmpWithLocation)));

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.Gps);

        Assert.Contains(result.RemovedTagNames, n => n == "Xmp:GPSLatitude");
        Assert.Contains(result.RemovedTagNames, n => n == "Xmp:GPSLongitude");
        Assert.Contains(result.RemovedTagNames, n => n == "Xmp:City");

        var xmp = image.GetXmpProfile();
        Assert.NotNull(xmp);
        var xml = xmp!.ToXDocument()!.ToString();
        Assert.DoesNotContain("GPSLatitude", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GPSLongitude", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Seattle", xml, StringComparison.Ordinal);
        Assert.Contains("Rating", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void StripMetadata_GpsCategory_RemovesIptcLocationRecords()
    {
        using var image = CreateImageWithExif();
        var iptc = new IptcProfile();
        iptc.SetValue(IptcTag.City, "Seattle");
        iptc.SetValue(IptcTag.Country, "USA");
        iptc.SetValue(IptcTag.Caption, "Keep me");
        image.SetProfile(iptc);

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.Gps);

        Assert.Contains(result.RemovedTagNames, n => n == "Iptc:City");
        Assert.Contains(result.RemovedTagNames, n => n == "Iptc:Country");

        var reloaded = image.GetIptcProfile();
        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.GetValue(IptcTag.City));
        Assert.Null(reloaded.GetValue(IptcTag.Country));
        Assert.NotNull(reloaded.GetValue(IptcTag.Caption));
    }

    [Fact]
    public void StripMetadata_All_RemovesXmpAndIptcProfiles()
    {
        using var image = CreateImageWithExif();
        image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes(XmpWithLocation)));
        var iptc = new IptcProfile();
        iptc.SetValue(IptcTag.City, "Seattle");
        image.SetProfile(iptc);

        var result = MetadataEditService.StripMetadata(image, MetadataStripCategory.All);

        Assert.Contains(result.RemovedTagNames, n => n == "Profile:xmp");
        Assert.Contains(result.RemovedTagNames, n => n == "Profile:iptc");
        Assert.Null(image.GetXmpProfile());
        Assert.Null(image.GetIptcProfile());
    }

    private const string XmpWithLocation =
        """
        <x:xmpmeta xmlns:x="adobe:ns:meta/">
          <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
            <rdf:Description rdf:about=""
                xmlns:exif="http://ns.adobe.com/exif/1.0/"
                xmlns:photoshop="http://ns.adobe.com/photoshop/1.0/"
                xmlns:xmp="http://ns.adobe.com/xap/1.0/"
                exif:GPSLatitude="47,36.0N"
                photoshop:City="Seattle">
              <exif:GPSLongitude>122,19.0W</exif:GPSLongitude>
              <xmp:Rating>5</xmp:Rating>
            </rdf:Description>
          </rdf:RDF>
        </x:xmpmeta>
        """;

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
