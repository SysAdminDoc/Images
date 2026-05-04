using System.Globalization;
using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// Read-only photo metadata summary for the viewer details panel. This deliberately stays
/// local and side-effect-free: no metadata writes, no map opens, no file mutation.
/// </summary>
public static class ImageMetadataService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ImageMetadataService));

    public static PhotoMetadata Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return PhotoMetadata.Empty;

        if (SupportedImageFormats.RequiresGhostscript(path))
            return PhotoMetadata.Empty;

        try
        {
            CodecRuntime.Configure();

            using var image = new MagickImage();
            image.Ping(new FileInfo(path), new MagickReadSettings
            {
                FrameIndex = 0,
                FrameCount = 1,
                BackgroundColor = MagickColors.White
            });

            var exif = image.GetExifProfile();
            if (exif is null)
                return PhotoMetadata.Empty;

            var rows = new List<MetadataFact>(6);

            var captured = MetadataDate.TryFromExif(
                Clean(ReadString(exif, ExifTag.DateTimeOriginal)),
                Clean(ReadString(exif, ExifTag.OffsetTimeOriginal)));
            if (captured.HasValue)
                rows.Add(new MetadataFact("Captured", captured.ToDisplay(CultureInfo.CurrentCulture)));

            var camera = FormatCamera(
                Clean(ReadString(exif, ExifTag.Make)),
                Clean(ReadString(exif, ExifTag.Model)));
            if (camera is not null)
                rows.Add(new MetadataFact("Camera", camera));

            var lens = Clean(ReadString(exif, ExifTag.LensModel));
            if (lens is not null)
                rows.Add(new MetadataFact("Lens", lens));

            var exposure = FormatExposure(
                ReadRational(exif, ExifTag.ExposureTime),
                ReadRational(exif, ExifTag.FNumber),
                ReadIso(exif));
            if (exposure is not null)
                rows.Add(new MetadataFact("Exposure", exposure));

            var focal = FormatFocalLength(
                ReadRational(exif, ExifTag.FocalLength),
                ReadUInt16(exif, ExifTag.FocalLengthIn35mmFilm));
            if (focal is not null)
                rows.Add(new MetadataFact("Focal", focal));

            var gps = FormatGps(
                ReadRationalArray(exif, ExifTag.GPSLatitude),
                Clean(ReadString(exif, ExifTag.GPSLatitudeRef)),
                ReadRationalArray(exif, ExifTag.GPSLongitude),
                Clean(ReadString(exif, ExifTag.GPSLongitudeRef)));
            if (gps is not null)
                rows.Add(new MetadataFact("GPS", gps));

            return rows.Count == 0 ? PhotoMetadata.Empty : new PhotoMetadata(rows);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.LogDebug(ex, "Metadata read failed for {Path}", path);
            return PhotoMetadata.Empty;
        }
    }

    private static string? ReadString(IExifProfile profile, ExifTag<string> tag)
        => profile.GetValue(tag)?.Value;

    private static Rational? ReadRational(IExifProfile profile, ExifTag<Rational> tag)
        => profile.GetValue(tag)?.Value;

    private static Rational[]? ReadRationalArray(IExifProfile profile, ExifTag<Rational[]> tag)
        => profile.GetValue(tag)?.Value;

    private static ushort? ReadUInt16(IExifProfile profile, ExifTag<ushort> tag)
        => profile.GetValue(tag)?.Value;

    private static uint? ReadUInt32(IExifProfile profile, ExifTag<uint> tag)
        => profile.GetValue(tag)?.Value;

    private static ushort[]? ReadUInt16Array(IExifProfile profile, ExifTag<ushort[]> tag)
        => profile.GetValue(tag)?.Value;

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

    private static string? FormatCamera(string? make, string? model)
    {
        if (make is null) return model;
        if (model is null) return make;

        return model.StartsWith(make, StringComparison.OrdinalIgnoreCase)
            ? model
            : $"{make} {model}";
    }

    private static string? FormatExposure(Rational? shutter, Rational? aperture, string? iso)
    {
        var parts = new List<string>(3);

        var shutterText = FormatShutter(shutter);
        if (shutterText is not null) parts.Add(shutterText);

        var apertureText = FormatAperture(aperture);
        if (apertureText is not null) parts.Add(apertureText);

        if (iso is not null) parts.Add(iso);

        return parts.Count == 0 ? null : string.Join("  ·  ", parts);
    }

    private static string? FormatShutter(Rational? value)
    {
        if (value is null) return null;

        var seconds = value.Value.ToDouble();
        if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
            return null;

        if (seconds >= 1)
            return $"{seconds:0.##} s";

        var denominator = (int)Math.Round(1 / seconds);
        return denominator <= 0 ? null : $"1/{denominator} s";
    }

    private static string? FormatAperture(Rational? value)
    {
        if (value is null) return null;
        var f = value.Value.ToDouble();
        return f > 0 && !double.IsNaN(f) && !double.IsInfinity(f)
            ? $"f/{f:0.#}"
            : null;
    }

    private static string? ReadIso(IExifProfile profile)
    {
        var ratings = ReadUInt16Array(profile, ExifTag.ISOSpeedRatings);
        if (ratings is { Length: > 0 } && ratings[0] > 0)
            return $"ISO {ratings[0]}";

        var speed = ReadUInt32(profile, ExifTag.ISOSpeed);
        return speed is > 0 ? $"ISO {speed.Value}" : null;
    }

    private static string? FormatFocalLength(Rational? focalLength, ushort? focalLength35mm)
    {
        var focal = focalLength?.ToDouble();
        if (focal is null || focal <= 0 || double.IsNaN(focal.Value) || double.IsInfinity(focal.Value))
            return focalLength35mm is > 0 ? $"{focalLength35mm.Value} mm equiv." : null;

        var text = $"{focal.Value:0.#} mm";
        return focalLength35mm is > 0
            ? $"{text} ({focalLength35mm.Value} mm equiv.)"
            : text;
    }

    private static string? FormatGps(Rational[]? latitude, string? latitudeRef, Rational[]? longitude, string? longitudeRef)
    {
        var lat = ToDecimalDegrees(latitude, latitudeRef);
        var lon = ToDecimalDegrees(longitude, longitudeRef);

        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            return null;

        return lat.HasValue && lon.HasValue
            ? $"{lat.Value:0.000000}, {lon.Value:0.000000}"
            : null;
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

        if (reference is not null && (reference.Equals("S", StringComparison.OrdinalIgnoreCase) ||
                                      reference.Equals("W", StringComparison.OrdinalIgnoreCase)))
        {
            value *= -1;
        }

        return value;
    }
}

public sealed record PhotoMetadata(IReadOnlyList<MetadataFact> Rows)
{
    public static PhotoMetadata Empty { get; } = new(Array.Empty<MetadataFact>());
}

public sealed record MetadataFact(string Label, string Value);
