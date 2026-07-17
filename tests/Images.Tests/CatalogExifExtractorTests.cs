using Images.Services;
using ImageMagick;

namespace Images.Tests;

public sealed class CatalogExifExtractorTests
{
    [Fact]
    public void Extract_NullProfile_ReturnsEmpty()
    {
        var facts = CatalogExifExtractor.Extract((IExifProfile?)null);

        Assert.Same(CatalogExifFacts.Empty, CatalogExifFacts.Empty);
        Assert.True(facts.IsEmpty);
        Assert.False(facts.HasGeo);
    }

    [Fact]
    public void Extract_NorthEastGps_ProducesPositiveDecimalDegrees()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, Dms(37, 48, 30));   // 37°48'30"
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitude, Dms(122, 25, 0));  // 122°25'00"
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.True(facts.HasGeo);
        Assert.Equal(37.8083, facts.Latitude!.Value, 3);
        Assert.Equal(122.4167, facts.Longitude!.Value, 3);
    }

    [Fact]
    public void Extract_SouthWestGps_NegatesDecimalDegrees()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, Dms(33, 51, 54));
        exif.SetValue(ExifTag.GPSLatitudeRef, "S");
        exif.SetValue(ExifTag.GPSLongitude, Dms(70, 39, 0));
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.True(facts.Latitude < 0);
        Assert.True(facts.Longitude < 0);
        Assert.Equal(-33.865, facts.Latitude!.Value, 3);
        Assert.Equal(-70.65, facts.Longitude!.Value, 3);
    }

    [Fact]
    public void Extract_LoneLatitudeWithoutLongitude_DropsGeo()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, Dms(10, 0, 0));
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.Null(facts.Latitude);
        Assert.Null(facts.Longitude);
        Assert.False(facts.HasGeo);
    }

    [Fact]
    public void Extract_OutOfRangeLatitude_DropsGeo()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitude, Dms(200, 0, 0));   // impossible
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitude, Dms(10, 0, 0));
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.False(facts.HasGeo);
    }

    [Fact]
    public void Extract_CameraAndExposureFields_Populate()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Canon");
        exif.SetValue(ExifTag.Model, "EOS R5");
        exif.SetValue(ExifTag.LensModel, "RF24-70mm F2.8 L IS USM");
        exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 400 });
        exif.SetValue(ExifTag.FocalLength, new Rational(50, 1));
        exif.SetValue(ExifTag.FNumber, new Rational(28, 10));
        exif.SetValue(ExifTag.ExposureTime, new Rational(1, 250));

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.Equal("Canon", facts.CameraMake);
        Assert.Equal("EOS R5", facts.CameraModel);
        Assert.Equal("RF24-70mm F2.8 L IS USM", facts.LensModel);
        Assert.Equal(400, facts.Iso);
        Assert.Equal(50, facts.FocalLengthMm!.Value, 3);
        Assert.Equal(2.8, facts.FNumber!.Value, 3);
        Assert.Equal(1.0 / 250.0, facts.ExposureSeconds!.Value, 6);
    }

    [Fact]
    public void Extract_CapturedTimeWithExplicitOffset_IsDeterministicUtc()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.DateTimeOriginal, "2021:07:15 14:30:00");
        exif.SetValue(ExifTag.OffsetTimeOriginal, "+00:00");

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.NotNull(facts.CapturedUtc);
        Assert.Equal(new DateTimeOffset(2021, 7, 15, 14, 30, 0, TimeSpan.Zero), facts.CapturedUtc!.Value);
    }

    [Fact]
    public void Extract_ZeroExposure_IsRejected()
    {
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.FNumber, new Rational(0, 1));

        var facts = CatalogExifExtractor.Extract(exif);

        Assert.Null(facts.FNumber);
    }

    private static Rational[] Dms(uint degrees, uint minutes, uint seconds)
        => [new Rational(degrees, 1), new Rational(minutes, 1), new Rational(seconds, 1)];
}
