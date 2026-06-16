using System.Globalization;
using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class MetadataDateTests
{
    [Fact]
    public void TryFromExif_WithPositiveOffset_PreservesOffsetAndDisplaysSign()
    {
        var value = MetadataDate.TryFromExif("2026:05:05 14:15:16", "+02:30");

        Assert.True(value.HasValue);
        Assert.True(value.HasOffset);
        Assert.Equal(new DateTimeOffset(2026, 5, 5, 14, 15, 16, TimeSpan.FromMinutes(150)), value.Value);
        Assert.Equal("05/05/2026 14:15:16 +02:30", value.ToDisplay(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryFromExif_WithNegativeOffset_PreservesOffsetAndDisplaysSign()
    {
        var value = MetadataDate.TryFromExif("2026:12:31 23:59:58", "-07:00");

        Assert.True(value.HasValue);
        Assert.True(value.HasOffset);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 58, TimeSpan.FromHours(-7)), value.Value);
        Assert.Equal("12/31/2026 23:59:58 -07:00", value.ToDisplay(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryFromExif_WithoutOffset_DoesNotInventOffsetText()
    {
        var value = MetadataDate.TryFromExif("2026:05:05 14:15:16", null);

        Assert.True(value.HasValue);
        Assert.False(value.HasOffset);
        Assert.Equal("05/05/2026 14:15:16", value.ToDisplay(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryFromExif_InvalidDate_ReturnsNone()
    {
        var value = MetadataDate.TryFromExif("not a date", "+02:00");

        Assert.False(value.HasValue);
        Assert.Equal("—", value.ToDisplay(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ImageMetadataServiceReadsCapturedOffsetFromJpegExif()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "offset-date.jpg");
        using var image = new MagickImage(MagickColors.Red, 8, 8)
        {
            Format = MagickFormat.Jpeg
        };
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.DateTimeOriginal, "2026:05:05 14:15:16");
        exif.SetValue(ExifTag.OffsetTimeOriginal, "+02:30");
        image.SetProfile(exif);
        image.Write(path);

        var metadata = ImageMetadataService.Read(path);

        var captured = Assert.Single(metadata.Rows, row => row.Label == "Captured");
        Assert.Contains("+02:30", captured.Value, StringComparison.Ordinal);
    }
}
