using ImageMagick;

namespace Images.Services;

/// <summary>
/// Structured EXIF facts the catalog indexes so geo/time/camera queries (trip detection,
/// near-duplicate stacking, smart-collection criteria) run against SQLite instead of
/// re-reading every file on disk. Pure extraction — no display formatting or UI concerns.
/// </summary>
public sealed record CatalogExifFacts(
    double? Latitude,
    double? Longitude,
    DateTimeOffset? CapturedUtc,
    string? CameraMake,
    string? CameraModel,
    string? LensModel,
    int? Iso,
    double? FocalLengthMm,
    double? FNumber,
    double? ExposureSeconds)
{
    public static readonly CatalogExifFacts Empty =
        new(null, null, null, null, null, null, null, null, null, null);

    /// <summary>True only when both axes resolved — a lone axis is never stored.</summary>
    public bool HasGeo => Latitude is not null && Longitude is not null;

    public bool IsEmpty =>
        Latitude is null && Longitude is null && CapturedUtc is null &&
        CameraMake is null && CameraModel is null && LensModel is null &&
        Iso is null && FocalLengthMm is null && FNumber is null && ExposureSeconds is null;
}

/// <summary>
/// Reads geo/time/camera EXIF into <see cref="CatalogExifFacts"/>. Mirrors the GPS
/// decimal-degree and ISO logic used by <see cref="ImageMetadataService"/> for the details
/// panel, but returns machine-usable values (doubles/ints/instants) rather than localized text.
/// </summary>
public static class CatalogExifExtractor
{
    public static CatalogExifFacts Extract(IMagickImage? image)
    {
        if (image is null)
            return CatalogExifFacts.Empty;

        try
        {
            return Extract(image.GetExifProfile());
        }
        catch (MagickException)
        {
            return CatalogExifFacts.Empty;
        }
    }

    public static CatalogExifFacts Extract(IExifProfile? exif)
    {
        if (exif is null)
            return CatalogExifFacts.Empty;

        var latitude = ToDecimalDegrees(
            ReadArray(exif, ExifTag.GPSLatitude),
            ReadString(exif, ExifTag.GPSLatitudeRef));
        var longitude = ToDecimalDegrees(
            ReadArray(exif, ExifTag.GPSLongitude),
            ReadString(exif, ExifTag.GPSLongitudeRef));

        // A coordinate is only meaningful as a pair and inside the valid range; drop otherwise.
        if (latitude is null || longitude is null ||
            Math.Abs(latitude.Value) > 90 || Math.Abs(longitude.Value) > 180)
        {
            latitude = null;
            longitude = null;
        }

        var captured = MetadataDate.TryFromExif(
            Clean(ReadString(exif, ExifTag.DateTimeOriginal)),
            Clean(ReadString(exif, ExifTag.OffsetTimeOriginal)));

        return new CatalogExifFacts(
            latitude,
            longitude,
            captured.Value?.ToUniversalTime(),
            Clean(ReadString(exif, ExifTag.Make)),
            Clean(ReadString(exif, ExifTag.Model)),
            Clean(ReadString(exif, ExifTag.LensModel)),
            ReadIso(exif),
            ReadPositiveRational(exif, ExifTag.FocalLength),
            ReadPositiveRational(exif, ExifTag.FNumber),
            ReadPositiveRational(exif, ExifTag.ExposureTime));
    }

    private static string? ReadString(IExifProfile profile, ExifTag<string> tag)
        => profile.GetValue(tag)?.Value;

    private static Rational[]? ReadArray(IExifProfile profile, ExifTag<Rational[]> tag)
        => profile.GetValue(tag)?.Value;

    private static int? ReadIso(IExifProfile profile)
    {
        var ratings = profile.GetValue(ExifTag.ISOSpeedRatings)?.Value;
        if (ratings is { Length: > 0 } && ratings[0] > 0)
            return ratings[0];

        var speed = profile.GetValue(ExifTag.ISOSpeed)?.Value;
        return speed is > 0 ? checked((int)Math.Min(speed.Value, int.MaxValue)) : null;
    }

    private static double? ReadPositiveRational(IExifProfile profile, ExifTag<Rational> tag)
    {
        var value = profile.GetValue(tag)?.Value;
        if (value is null)
            return null;

        var d = value.Value.ToDouble();
        return double.IsFinite(d) && d > 0 ? d : null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = new string(value
            .Select(c => char.IsControl(c) ? ' ' : c)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static double? ToDecimalDegrees(Rational[]? dms, string? reference)
    {
        if (dms is not { Length: >= 3 })
            return null;

        var degrees = dms[0].ToDouble();
        var minutes = dms[1].ToDouble();
        var seconds = dms[2].ToDouble();
        if (degrees < 0 || minutes < 0 || seconds < 0 || minutes >= 60 || seconds >= 60)
            return null;

        var value = degrees + (minutes / 60d) + (seconds / 3600d);
        if (double.IsNaN(value) || double.IsInfinity(value))
            return null;

        if (reference is not null &&
            (reference.Equals("S", StringComparison.OrdinalIgnoreCase) ||
             reference.Equals("W", StringComparison.OrdinalIgnoreCase)))
        {
            value *= -1;
        }

        return value;
    }
}
