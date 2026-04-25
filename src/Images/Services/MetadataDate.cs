using System.Globalization;

namespace Images.Services;

/// <summary>
/// I-04 / NEXT-11 defensive scaffolding — wraps <see cref="DateTimeOffset"/> for metadata
/// display. EXIF's <c>DateTimeOriginal</c> is local-time-string-no-timezone; <c>OffsetTimeOriginal</c>
/// (2016+) carries the offset. Parsing both into one type at ingest lets later UI never
/// accidentally assume UTC or local-without-offset.
/// Actual EXIF parsing lands in v0.2.x when the metadata overlay ships; this type is the
/// beachhead so we never bake <see cref="DateTime"/> into a signature that'll need a compat
/// break to swap later.
/// </summary>
public readonly struct MetadataDate : IEquatable<MetadataDate>
{
    /// <summary>The instant + offset. Null means "no date metadata on this file".</summary>
    public DateTimeOffset? Value { get; }

    /// <summary>True when the original source had an explicit timezone (EXIF <c>OffsetTimeOriginal</c>).</summary>
    public bool HasOffset { get; }

    public MetadataDate(DateTimeOffset value, bool hasOffset)
    {
        Value = value;
        HasOffset = hasOffset;
    }

    public static MetadataDate None => default;

    public bool HasValue => Value.HasValue;

    /// <summary>
    /// Format for display. Uses the supplied culture's short date + long time; appends UTC
    /// offset (e.g. "+02:00") only if the source metadata actually carried one — otherwise it'd
    /// be a fiction.
    /// </summary>
    public string ToDisplay(CultureInfo? culture = null)
    {
        if (!Value.HasValue) return "—";
        culture ??= CultureInfo.CurrentCulture;
        var local = Value.Value;
        var datePart = local.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
        var timePart = local.ToString(culture.DateTimeFormat.LongTimePattern, culture);
        return HasOffset
            ? $"{datePart} {timePart} {local.Offset:hh\\:mm}"
            : $"{datePart} {timePart}";
    }

    /// <summary>
    /// Parse an EXIF-style "yyyy:MM:dd HH:mm:ss" + optional offset string into a MetadataDate.
    /// Returns <see cref="None"/> if parsing fails — callers should treat that as "no date".
    /// </summary>
    public static MetadataDate TryFromExif(string? dateTimeOriginal, string? offsetTimeOriginal)
    {
        if (string.IsNullOrWhiteSpace(dateTimeOriginal)) return None;
        // EXIF uses colon separators for the date portion; convert to ISO for parsing.
        var normalized = dateTimeOriginal.Length >= 10
            ? dateTimeOriginal[..4] + "-" + dateTimeOriginal[5..7] + "-" + dateTimeOriginal[8..]
            : dateTimeOriginal;

        if (!string.IsNullOrWhiteSpace(offsetTimeOriginal))
        {
            if (DateTimeOffset.TryParseExact(
                    normalized + " " + offsetTimeOriginal,
                    "yyyy-MM-dd HH:mm:ss zzz",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var dto))
            {
                return new MetadataDate(dto, hasOffset: true);
            }
        }

        if (DateTime.TryParseExact(
                normalized,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dt))
        {
            return new MetadataDate(new DateTimeOffset(dt), hasOffset: false);
        }

        return None;
    }

    public bool Equals(MetadataDate other) => Value == other.Value && HasOffset == other.HasOffset;
    public override bool Equals(object? obj) => obj is MetadataDate m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Value, HasOffset);
    public static bool operator ==(MetadataDate a, MetadataDate b) => a.Equals(b);
    public static bool operator !=(MetadataDate a, MetadataDate b) => !a.Equals(b);
}
