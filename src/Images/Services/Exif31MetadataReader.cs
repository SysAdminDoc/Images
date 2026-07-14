using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Images.Services;

/// <summary>
/// Reads the Exif 3.1 fields that Magick.NET 14 does not yet expose as named tags.
/// Exif 3.1 deliberately keeps the ExifVersion payload at "0300", so the reader also
/// validates each new field's type/shape and rejects the known Samsung/GE tag collisions.
/// </summary>
internal static class Exif31MetadataReader
{
    private const int MaxJpegSegments = 4096;
    private const ushort ExifIfdPointerTag = 0x8769;
    private const ushort MakeTag = 0x010f;
    private const ushort ExifVersionTag = 0x9000;
    private const ushort LearningOptOutInTag = 0x9287;
    private const ushort DevelopmentTypeTag = 0xa40d;
    private const ushort DevelopmentDescriptionTag = 0xa40e;
    private const ushort DistortionCorrectionTag = 0xa40f;
    private const ushort ChromaticAberrationCorrectionTag = 0xa410;
    private const ushort ShadingCorrectionTag = 0xa411;
    private const ushort NoiseReductionTag = 0xa412;

    private const ushort TypeAscii = 2;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeUndefined = 7;
    private const ushort TypeUtf8 = 129;
    private const int MaxIfdEntries = 4096;
    private const int MaxEntryBytes = 64 * 1024;
    private const int MaxLearningPairs = 256;
    private const int MaxDescriptionCharacters = 512;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static Exif31Metadata? Read(Stream source, byte[]? normalizedProfileBytes)
    {
        var embeddedProfile = TryReadJpegExifProfile(source);
        return Read(embeddedProfile) ?? Read(normalizedProfileBytes);
    }

    public static Exif31Metadata? Read(byte[]? profileBytes)
    {
        if (profileBytes is null || profileBytes.Length < 14)
            return null;

        var reader = TiffReader.TryCreate(profileBytes);
        if (reader is null ||
            !reader.TryReadIfd(reader.FirstIfdOffset, out var primaryIfd) ||
            !TryReadExifIfd(reader, primaryIfd, out var exifIfd) ||
            !HasCompatibleVersion(reader, exifIfd))
        {
            return null;
        }

        var make = TryReadText(reader, primaryIfd.GetValueOrDefault(MakeTag), allowAscii: true);
        var isSamsung = StartsWithVendor(make, "Samsung");
        var isGe = StartsWithVendor(make, "GE");

        var learningEntryPresent = exifIfd.TryGetValue(LearningOptOutInTag, out var learningEntry);
        IReadOnlyList<ExifLearningUse> learningUses = Array.Empty<ExifLearningUse>();
        var learningValid = learningEntryPresent &&
                            TryReadLearningUses(reader, learningEntry!, out learningUses);

        var developmentEntryPresent = exifIfd.TryGetValue(DevelopmentTypeTag, out var developmentEntry);
        ushort developmentValue = 0;
        var developmentReadable = developmentEntryPresent &&
                                  TryReadSingleShort(reader, developmentEntry!, out developmentValue);
        var developmentDefined = developmentReadable &&
                                 IsDefinedDevelopmentByte((byte)(developmentValue >> 8)) &&
                                 IsDefinedDevelopmentByte((byte)developmentValue);

        var description = TryReadText(
            reader,
            exifIfd.GetValueOrDefault(DevelopmentDescriptionTag),
            allowAscii: false);
        var descriptionIsExif31 = description is not null;

        var distortion = TryReadOptionalShort(reader, exifIfd, DistortionCorrectionTag);
        var chromaticAberration = TryReadOptionalShort(reader, exifIfd, ChromaticAberrationCorrectionTag);
        var shading = TryReadOptionalShort(reader, exifIfd, ShadingCorrectionTag);
        var noiseReduction = TryReadOptionalShort(reader, exifIfd, NoiseReductionTag);
        var hasCorrection = distortion.HasValue || chromaticAberration.HasValue ||
                            shading.HasValue || noiseReduction.HasValue;

        // Exif 3.1 retains "0300" in ExifVersion. These shape checks are therefore the
        // actual format gate, while the vendor checks prevent the documented legacy IDs
        // from being presented as standardized metadata.
        var hasExif31Signature =
            (learningValid && !isSamsung) ||
            (developmentDefined && !isGe) ||
            descriptionIsExif31 ||
            hasCorrection;
        if (!hasExif31Signature)
            return null;

        var safeLearningUses = learningValid && !isSamsung
            ? learningUses
            : Array.Empty<ExifLearningUse>();
        var learningInvalid = learningEntryPresent && !isSamsung && !learningValid;

        ExifDevelopmentType? development = null;
        if (developmentReadable && (!isGe || descriptionIsExif31))
        {
            development = new ExifDevelopmentType(
                (byte)(developmentValue >> 8),
                (byte)developmentValue);
        }

        return new Exif31Metadata(
            safeLearningUses,
            learningInvalid,
            development,
            description,
            distortion,
            chromaticAberration,
            shading,
            noiseReduction);
    }

    private static byte[]? TryReadJpegExifProfile(Stream source)
    {
        if (!source.CanRead || !source.CanSeek)
            return null;

        var originalPosition = source.Position;
        try
        {
            source.Position = 0;
            if (source.ReadByte() != 0xff || source.ReadByte() != 0xd8)
                return null;

            Span<byte> lengthBytes = stackalloc byte[2];
            for (var segment = 0; segment < MaxJpegSegments; segment++)
            {
                var prefix = source.ReadByte();
                while (prefix != -1 && prefix != 0xff)
                    prefix = source.ReadByte();
                if (prefix == -1)
                    return null;

                var marker = source.ReadByte();
                while (marker == 0xff)
                    marker = source.ReadByte();
                if (marker is -1 or 0xd9 or 0xda)
                    return null;
                if (marker is 0x01 or >= 0xd0 and <= 0xd7)
                    continue;

                if (!TryReadExactly(source, lengthBytes))
                    return null;
                var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
                if (segmentLength < 2)
                    return null;

                var payloadLength = segmentLength - 2;
                if (marker == 0xe1 && payloadLength >= 6)
                {
                    var profile = new byte[payloadLength];
                    if (!TryReadExactly(source, profile))
                        return null;
                    if (profile.AsSpan().StartsWith("Exif\0\0"u8))
                        return profile;
                    continue;
                }

                if (source.Length - source.Position < payloadLength)
                    return null;
                source.Position += payloadLength;
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            source.Position = originalPosition;
        }
    }

    private static bool TryReadExactly(Stream source, Span<byte> destination)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = source.Read(destination[total..]);
            if (read == 0)
                return false;
            total += read;
        }

        return true;
    }

    private static bool TryReadExifIfd(
        TiffReader reader,
        IReadOnlyDictionary<ushort, TiffEntry> primaryIfd,
        out IReadOnlyDictionary<ushort, TiffEntry> exifIfd)
    {
        exifIfd = null!;
        if (!primaryIfd.TryGetValue(ExifIfdPointerTag, out var pointer) ||
            pointer.Type != TypeLong || pointer.Count != 1 ||
            !reader.TryReadUInt32Value(pointer, out var offset))
        {
            return false;
        }

        return reader.TryReadIfd(offset, out exifIfd);
    }

    private static bool HasCompatibleVersion(
        TiffReader reader,
        IReadOnlyDictionary<ushort, TiffEntry> exifIfd)
    {
        if (!exifIfd.TryGetValue(ExifVersionTag, out var entry) ||
            entry.Type != TypeUndefined || entry.Count != 4 ||
            !reader.TryGetData(entry, out var data))
        {
            return false;
        }

        // CIPA DC-008-2026 specifies "0300" for Exif 3.1. Accept "0310" as a
        // defensive interoperability allowance for writers that encode the revision.
        return data.SequenceEqual("0300"u8) || data.SequenceEqual("0310"u8);
    }

    private static bool TryReadLearningUses(
        TiffReader reader,
        TiffEntry entry,
        out IReadOnlyList<ExifLearningUse> uses)
    {
        uses = Array.Empty<ExifLearningUse>();
        if (entry.Type != TypeUndefined || !reader.TryGetData(entry, out var data) ||
            data.Length < 6 || (data.Length & 1) != 0)
        {
            return false;
        }

        var pairCount = reader.ReadUInt16(data);
        if (pairCount is 0 or > MaxLearningPairs || data.Length != 2 + (pairCount * 4))
            return false;

        var result = new List<ExifLearningUse>(pairCount);
        var seenUsages = new HashSet<ushort>();
        for (var index = 0; index < pairCount; index++)
        {
            var offset = 2 + (index * 4);
            var usage = reader.ReadUInt16(data[offset..]);
            var intention = reader.ReadUInt16(data[(offset + 2)..]);
            if ((index == 0 && usage != 0) || !seenUsages.Add(usage))
                return false;

            result.Add(new ExifLearningUse(usage, intention));
        }

        uses = result;
        return true;
    }

    private static ushort? TryReadOptionalShort(
        TiffReader reader,
        IReadOnlyDictionary<ushort, TiffEntry> exifIfd,
        ushort tag)
        => exifIfd.TryGetValue(tag, out var entry) &&
           TryReadSingleShort(reader, entry, out var value)
            ? value
            : null;

    private static bool TryReadSingleShort(TiffReader reader, TiffEntry entry, out ushort value)
    {
        value = 0;
        if (entry.Type != TypeShort || entry.Count != 1 ||
            !reader.TryGetData(entry, out var data) || data.Length != 2)
        {
            return false;
        }

        value = reader.ReadUInt16(data);
        return true;
    }

    private static string? TryReadText(TiffReader reader, TiffEntry? entry, bool allowAscii)
    {
        if (entry is null ||
            (entry.Type != TypeUtf8 && (!allowAscii || entry.Type != TypeAscii)) ||
            !reader.TryGetData(entry, out var data) ||
            data.Length == 0 || data[^1] != 0)
        {
            return null;
        }

        try
        {
            var value = entry.Type == TypeUtf8
                ? StrictUtf8.GetString(data[..^1])
                : Encoding.ASCII.GetString(data[..^1]);
            if (value.IndexOf('\0') >= 0)
                return null;

            var cleaned = new string(value
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            if (cleaned.Length == 0)
                return null;

            return cleaned.Length <= MaxDescriptionCharacters
                ? cleaned
                : string.Concat(cleaned.AsSpan(0, MaxDescriptionCharacters - 1), "…");
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsDefinedDevelopmentByte(byte value) => value is 1 or 2 or 4;

    private static bool StartsWithVendor(string? value, string vendor)
        => value is not null &&
           (value.Equals(vendor, StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(vendor + " ", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(vendor + "-", StringComparison.OrdinalIgnoreCase));

    private sealed class TiffReader
    {
        private readonly byte[] _data;
        private readonly int _tiffStart;
        private readonly bool _littleEndian;

        private TiffReader(byte[] data, int tiffStart, bool littleEndian, uint firstIfdOffset)
        {
            _data = data;
            _tiffStart = tiffStart;
            _littleEndian = littleEndian;
            FirstIfdOffset = firstIfdOffset;
        }

        public uint FirstIfdOffset { get; }

        public static TiffReader? TryCreate(byte[] data)
        {
            var tiffStart = data.AsSpan().StartsWith("Exif\0\0"u8) ? 6 : 0;
            if (data.Length - tiffStart < 8)
                return null;

            var header = data.AsSpan(tiffStart);
            var littleEndian = header[0] == (byte)'I' && header[1] == (byte)'I';
            var bigEndian = header[0] == (byte)'M' && header[1] == (byte)'M';
            if (!littleEndian && !bigEndian)
                return null;

            var magic = littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(header[2..])
                : BinaryPrimitives.ReadUInt16BigEndian(header[2..]);
            if (magic != 42)
                return null;

            var firstIfdOffset = littleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(header[4..])
                : BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
            return new TiffReader(data, tiffStart, littleEndian, firstIfdOffset);
        }

        public bool TryReadIfd(uint relativeOffset, out IReadOnlyDictionary<ushort, TiffEntry> entries)
        {
            entries = null!;
            if (!TryAbsoluteOffset(relativeOffset, 2, out var absoluteOffset))
                return false;

            var count = ReadUInt16(_data.AsSpan(absoluteOffset));
            if (count > MaxIfdEntries)
                return false;

            var tableBytes = (long)count * 12;
            if (absoluteOffset + 2L + tableBytes + 4 > _data.Length)
                return false;

            var result = new Dictionary<ushort, TiffEntry>(count);
            for (var index = 0; index < count; index++)
            {
                var entryOffset = absoluteOffset + 2 + (index * 12);
                var span = _data.AsSpan(entryOffset, 12);
                var tag = ReadUInt16(span);
                var type = ReadUInt16(span[2..]);
                var valueCount = ReadUInt32(span[4..]);
                if (!result.TryAdd(tag, new TiffEntry(type, valueCount, entryOffset + 8)))
                    return false;
            }

            entries = result;
            return true;
        }

        public bool TryReadUInt32Value(TiffEntry entry, out uint value)
        {
            value = 0;
            if (entry.Type != TypeLong || entry.Count != 1 ||
                !TryGetData(entry, out var data) || data.Length != 4)
            {
                return false;
            }

            value = ReadUInt32(data);
            return true;
        }

        public bool TryGetData(TiffEntry entry, out ReadOnlySpan<byte> data)
        {
            data = default;
            var width = TypeWidth(entry.Type);
            var byteCount = (long)width * entry.Count;
            if (width == 0 || byteCount is <= 0 or > MaxEntryBytes)
                return false;

            int absoluteOffset;
            if (byteCount <= 4)
            {
                absoluteOffset = entry.ValueFieldOffset;
            }
            else
            {
                var relativeOffset = ReadUInt32(_data.AsSpan(entry.ValueFieldOffset, 4));
                if (!TryAbsoluteOffset(relativeOffset, (int)byteCount, out absoluteOffset))
                    return false;
            }

            if (absoluteOffset < 0 || absoluteOffset + byteCount > _data.Length)
                return false;

            data = _data.AsSpan(absoluteOffset, (int)byteCount);
            return true;
        }

        public ushort ReadUInt16(ReadOnlySpan<byte> data)
            => _littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(data)
                : BinaryPrimitives.ReadUInt16BigEndian(data);

        private uint ReadUInt32(ReadOnlySpan<byte> data)
            => _littleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(data)
                : BinaryPrimitives.ReadUInt32BigEndian(data);

        private bool TryAbsoluteOffset(uint relativeOffset, int requiredBytes, out int absoluteOffset)
        {
            absoluteOffset = 0;
            var candidate = (long)_tiffStart + relativeOffset;
            if (candidate < _tiffStart || candidate + requiredBytes > _data.Length)
                return false;

            absoluteOffset = (int)candidate;
            return true;
        }

        private static int TypeWidth(ushort type) => type switch
        {
            1 or TypeAscii or TypeUndefined or TypeUtf8 => 1,
            TypeShort => 2,
            TypeLong or 9 => 4,
            5 or 10 => 8,
            _ => 0
        };
    }

    private sealed record TiffEntry(ushort Type, uint Count, int ValueFieldOffset);
}

internal sealed record Exif31Metadata(
    IReadOnlyList<ExifLearningUse> LearningUses,
    bool LearningUseInvalid,
    ExifDevelopmentType? Development,
    string? DevelopmentDescription,
    ushort? DistortionCorrection,
    ushort? ChromaticAberrationCorrection,
    ushort? ShadingCorrection,
    ushort? NoiseReduction);

internal readonly record struct ExifLearningUse(ushort Usage, ushort Intention);

internal readonly record struct ExifDevelopmentType(byte Characteristic, byte FactoryDifference);
