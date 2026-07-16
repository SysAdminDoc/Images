using System.Globalization;
using System.IO;
using Images.Localization;
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

            using var stream = OpenSharedRead(path);
            using var image = new MagickImage();
            image.Ping(stream, new MagickReadSettings
            {
                FrameIndex = 0,
                FrameCount = 1,
                BackgroundColor = MagickColors.White
            });

            var rows = new List<MetadataFact>(16);
            var exif = image.GetExifProfile();
            if (exif is not null)
            {
                var captured = MetadataDate.TryFromExif(
                    Clean(ReadString(exif, ExifTag.DateTimeOriginal)),
                    Clean(ReadString(exif, ExifTag.OffsetTimeOriginal)));
                if (captured.HasValue)
                    rows.Add(new MetadataFact(Strings.MetadataCaptured, captured.ToDisplay(CultureInfo.CurrentCulture)));

                var camera = FormatCamera(
                    Clean(ReadString(exif, ExifTag.Make)),
                    Clean(ReadString(exif, ExifTag.Model)));
                if (camera is not null)
                    rows.Add(new MetadataFact(Strings.MetadataCamera, camera));

                var lens = Clean(ReadString(exif, ExifTag.LensModel));
                if (lens is not null)
                    rows.Add(new MetadataFact(Strings.MetadataLens, lens));

                var exposure = FormatExposure(
                    ReadRational(exif, ExifTag.ExposureTime),
                    ReadRational(exif, ExifTag.FNumber),
                    ReadIso(exif));
                if (exposure is not null)
                    rows.Add(new MetadataFact(Strings.MetadataExposure, exposure));

                var focal = FormatFocalLength(
                    ReadRational(exif, ExifTag.FocalLength),
                    ReadUInt16(exif, ExifTag.FocalLengthIn35mmFilm));
                if (focal is not null)
                    rows.Add(new MetadataFact(Strings.MetadataFocal, focal));

                AppendExif31Rows(rows, Exif31MetadataReader.Read(stream, exif.ToByteArray()));

                var gps = FormatGps(
                    ReadRationalArray(exif, ExifTag.GPSLatitude),
                    Clean(ReadString(exif, ExifTag.GPSLatitudeRef)),
                    ReadRationalArray(exif, ExifTag.GPSLongitude),
                    Clean(ReadString(exif, ExifTag.GPSLongitudeRef)));
                if (gps is not null)
                    rows.Add(new MetadataFact(Strings.MetadataGps, gps));
            }

            AppendGainMapRows(rows, GainMapInspectionService.Inspect(path));

            return rows.Count == 0 ? PhotoMetadata.Empty : new PhotoMetadata(rows);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.LogDebug(ex, "Metadata read failed for {Path}", path);
            return PhotoMetadata.Empty;
        }
    }

    private static FileStream OpenSharedRead(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

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
            return Strings.Format(nameof(Strings.MetadataShutterSecondsFormat), seconds);

        var denominator = (int)Math.Round(1 / seconds);
        return denominator <= 0
            ? null
            : Strings.Format(nameof(Strings.MetadataShutterFractionFormat), denominator);
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
            return Strings.Format(nameof(Strings.MetadataIsoFormat), ratings[0]);

        var speed = ReadUInt32(profile, ExifTag.ISOSpeed);
        return speed is > 0 ? Strings.Format(nameof(Strings.MetadataIsoFormat), speed.Value) : null;
    }

    private static string? FormatFocalLength(Rational? focalLength, ushort? focalLength35mm)
    {
        var focal = focalLength?.ToDouble();
        if (focal is null || focal <= 0 || double.IsNaN(focal.Value) || double.IsInfinity(focal.Value))
            return focalLength35mm is > 0
                ? Strings.Format(nameof(Strings.MetadataFocalEquivalentFormat), focalLength35mm.Value)
                : null;

        var text = Strings.Format(nameof(Strings.MetadataFocalMillimetersFormat), focal.Value);
        return focalLength35mm is > 0
            ? Strings.Format(nameof(Strings.MetadataFocalWithEquivalentFormat), text, focalLength35mm.Value)
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

    private static void AppendExif31Rows(List<MetadataFact> rows, Exif31Metadata? metadata)
    {
        if (metadata is null)
            return;

        if (metadata.LearningUses.Count > 0)
        {
            var values = metadata.LearningUses
                .Take(8)
                .Select(item => Strings.Format(
                    nameof(Strings.MetadataLearningUseEntryFormat),
                    FormatLearningUsage(item.Usage),
                    FormatLearningIntention(item.Intention)))
                .ToList();
            if (metadata.LearningUses.Count > values.Count)
            {
                values.Add(Strings.Format(
                    nameof(Strings.MetadataMoreItemsFormat),
                    metadata.LearningUses.Count - values.Count));
            }

            rows.Add(new MetadataFact(Strings.MetadataLearningUse, string.Join("; ", values)));
        }
        else if (metadata.LearningUseInvalid)
        {
            rows.Add(new MetadataFact(Strings.MetadataLearningUse, Strings.MetadataInvalidValue));
        }

        if (metadata.Development is { } development)
        {
            rows.Add(new MetadataFact(
                Strings.MetadataDevelopment,
                Strings.Format(
                    nameof(Strings.MetadataDevelopmentFormat),
                    FormatDevelopmentCharacteristic(development.Characteristic),
                    FormatFactoryDifference(development.FactoryDifference))));
        }

        if (metadata.DevelopmentDescription is not null)
        {
            rows.Add(new MetadataFact(
                Strings.MetadataDevelopmentDescription,
                metadata.DevelopmentDescription));
        }

        AppendCorrectionRow(rows, Strings.MetadataDistortionCorrection, metadata.DistortionCorrection);
        AppendCorrectionRow(rows, Strings.MetadataChromaticAberrationCorrection, metadata.ChromaticAberrationCorrection);
        AppendCorrectionRow(rows, Strings.MetadataShadingCorrection, metadata.ShadingCorrection);

        if (metadata.NoiseReduction.HasValue)
        {
            rows.Add(new MetadataFact(
                Strings.MetadataNoiseReduction,
                metadata.NoiseReduction.Value switch
                {
                    0 => Strings.MetadataCorrectionNotApplied,
                    1 => Strings.MetadataNoiseReductionLow,
                    2 => Strings.MetadataNoiseReductionNormal,
                    3 => Strings.MetadataNoiseReductionHigh,
                    var value => FormatUnknown(value)
                }));
        }
    }

    private static void AppendGainMapRows(List<MetadataFact> rows, GainMapInspection gainMap)
    {
        if (!gainMap.Present)
            return;

        var flavor = gainMap.Flavor switch
        {
            GainMapFlavor.UltraHdr => Strings.MetadataGainMapUltraHdr,
            GainMapFlavor.AppleGainMap => Strings.MetadataGainMapApple,
            GainMapFlavor.Iso21496 => Strings.MetadataGainMapIso21496,
            _ => Strings.MetadataGainMapMetadata,
        };

        rows.Add(new MetadataFact(
            Strings.MetadataGainMap,
            gainMap.Version is { Length: > 0 } version
                ? Strings.Format(nameof(Strings.MetadataGainMapWithVersionFormat), flavor, version)
                : flavor));

        if (gainMap.MinBoostStops is { } min && gainMap.MaxBoostStops is { } max)
        {
            rows.Add(new MetadataFact(
                Strings.MetadataGainMapBoost,
                Strings.Format(nameof(Strings.MetadataGainMapBoostFormat), min, max)));
        }
    }

    private static void AppendCorrectionRow(List<MetadataFact> rows, string label, ushort? value)
    {
        if (!value.HasValue)
            return;

        rows.Add(new MetadataFact(
            label,
            value.Value switch
            {
                0 => Strings.MetadataCorrectionNotApplied,
                1 => Strings.MetadataCorrectionApplied,
                var unknown => FormatUnknown(unknown)
            }));
    }

    private static string FormatLearningUsage(ushort value) => value switch
    {
        0 => Strings.MetadataLearningUseOther,
        1 => Strings.MetadataLearningUseNonGenerative,
        2 => Strings.MetadataLearningUseGenerative,
        3 => Strings.MetadataLearningUseDataMining,
        4 => Strings.MetadataLearningUseFoundationModel,
        _ => Strings.Format(nameof(Strings.MetadataUnknownUseFormat), value)
    };

    private static string FormatLearningIntention(ushort value) => value switch
    {
        0 => Strings.MetadataLearningIntentOptOut,
        1 => Strings.MetadataLearningIntentOptIn,
        2 => Strings.MetadataLearningIntentUnspecified,
        _ => FormatUnknown(value)
    };

    private static string FormatDevelopmentCharacteristic(byte value) => value switch
    {
        1 => Strings.MetadataDevelopmentAccurate,
        2 => Strings.MetadataDevelopmentSmallDifferences,
        4 => Strings.MetadataDevelopmentExtremeDifferences,
        _ => FormatUnknown(value)
    };

    private static string FormatFactoryDifference(byte value) => value switch
    {
        1 => Strings.MetadataDevelopmentFactoryDefaults,
        2 => Strings.MetadataDevelopmentNotFactoryDefaults,
        4 => Strings.MetadataDevelopmentFactoryUnknown,
        _ => FormatUnknown(value)
    };

    private static string FormatUnknown(ushort value)
        => Strings.Format(nameof(Strings.MetadataUnknownValueFormat), value);

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
