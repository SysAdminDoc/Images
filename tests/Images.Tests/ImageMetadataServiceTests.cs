using System.Buffers.Binary;
using System.IO;
using System.Text;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageMetadataServiceTests
{
    [Fact]
    public void Read_NullPath_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(null!);
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_EmptyPath_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(string.Empty);
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_NonexistentFile_ReturnsEmpty()
    {
        var result = ImageMetadataService.Read(@"C:\__nonexistent_test_path__\image.jpg");
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Fact]
    public void Read_GhostscriptFormat_ReturnsEmpty()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("test.pdf", "dummy");
        var result = ImageMetadataService.Read(path);
        Assert.Equal(PhotoMetadata.Empty, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_Exif31Metadata_DecodesAndDoesNotModifyFile(bool littleEndian)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, littleEndian ? "exif31-le.jpg" : "exif31-be.jpg");
        Exif31TestFixture.WriteJpeg(
            path,
            littleEndian,
            "0300",
            "Test Camera",
            Exif31TestFixture.Undefined(0x9287, littleEndian, 2, 0, 2, 2, 0),
            Exif31TestFixture.Short(0xa40d, littleEndian, 0x0202),
            Exif31TestFixture.Utf8(0xa40e, "Local tone and perspective"),
            Exif31TestFixture.Short(0xa40f, littleEndian, 1),
            Exif31TestFixture.Short(0xa410, littleEndian, 0),
            Exif31TestFixture.Short(0xa411, littleEndian, 7),
            Exif31TestFixture.Short(0xa412, littleEndian, 3));
        var before = File.ReadAllBytes(path);

        var result = ImageMetadataService.Read(path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(
            "Other or unspecified uses: Unspecified; Generative AI/ML training: Opt-out",
            FindValue(result, "AI/ML use"));
        Assert.Equal("Small differences; Not factory defaults", FindValue(result, "Development"));
        Assert.Equal("Local tone and perspective", FindValue(result, "Development details"));
        Assert.Equal("Applied", FindValue(result, "Distortion correction"));
        Assert.Equal("Not applied", FindValue(result, "Chromatic correction"));
        Assert.Equal("Unknown (7)", FindValue(result, "Shading correction"));
        Assert.Equal("High strength", FindValue(result, "Noise reduction"));
    }

    [Fact]
    public void Read_Exif31ReservedLearningValues_RemainExplicit()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "exif31-reserved.jpg");
        Exif31TestFixture.WriteJpeg(
            path,
            littleEndian: true,
            "0300",
            "Test Camera",
            Exif31TestFixture.Undefined(0x9287, littleEndian: true, 2, 0, 2, 9, 8));

        var result = ImageMetadataService.Read(path);

        Assert.Equal(
            "Other or unspecified uses: Unspecified; Unknown use (9): Unknown (8)",
            FindValue(result, "AI/ML use"));
    }

    [Fact]
    public void Read_MalformedExif31LearningValue_IsExplicitWhenProfileHasSafeSignature()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "exif31-invalid-learning.jpg");
        Exif31TestFixture.WriteJpeg(
            path,
            littleEndian: true,
            "0300",
            "Test Camera",
            Exif31TestFixture.Undefined(0x9287, littleEndian: true, 3, 0, 2),
            Exif31TestFixture.Short(0xa40f, littleEndian: true, 1));

        var result = ImageMetadataService.Read(path);

        Assert.Equal("Invalid metadata", FindValue(result, "AI/ML use"));
        Assert.Equal("Applied", FindValue(result, "Distortion correction"));
    }

    [Theory]
    [InlineData("Samsung Electronics", 0x9287)]
    [InlineData("GE DIGITAL CAMERA", 0xa40d)]
    public void Read_LegacyVendorCollision_IsNotPresentedAsExif31(string make, ushort collisionTag)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, $"legacy-{collisionTag:x4}.jpg");
        var collision = collisionTag == 0x9287
            ? Exif31TestFixture.Undefined(collisionTag, littleEndian: true, 1, 0, 2)
            : Exif31TestFixture.Short(collisionTag, littleEndian: true, 0x0101);
        Exif31TestFixture.WriteJpeg(path, littleEndian: true, "0300", make, collision);

        var result = ImageMetadataService.Read(path);

        Assert.DoesNotContain(result.Rows, row => row.Label == "AI/ML use");
        Assert.DoesNotContain(result.Rows, row => row.Label == "Development");
    }

    [Fact]
    public void Read_PreExif3Profile_DoesNotInterpretNewTagIds()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "legacy-version.jpg");
        Exif31TestFixture.WriteJpeg(
            path,
            littleEndian: true,
            "0230",
            "Test Camera",
            Exif31TestFixture.Short(0xa40f, littleEndian: true, 1));

        var result = ImageMetadataService.Read(path);

        Assert.DoesNotContain(result.Rows, row => row.Label == "Distortion correction");
    }

    [Fact]
    public void Read_UltraHdrGainMapJpeg_SurfacesGainMapRows()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "ultrahdr.jpg");
        var xmp = Encoding.UTF8.GetBytes(
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" xmlns:hdrgm=\"http://ns.adobe.com/hdr-gain-map/1.0/\" " +
            "hdrgm:Version=\"1.0\" hdrgm:GainMapMin=\"0.0\" hdrgm:GainMapMax=\"3.0\"></x:xmpmeta>");
        using (var image = new MagickImage(MagickColors.Orange, 16, 8) { Format = MagickFormat.Jpeg })
        {
            image.SetProfile(new ImageProfile("xmp", xmp));
            image.Write(path);
        }

        var result = ImageMetadataService.Read(path);

        var gainMap = FindValue(result, "HDR gain map");
        Assert.Contains("gain map", gainMap, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("0.0 to 3.0 stops", FindValue(result, "Content boost"));
    }

    [Fact]
    public void Read_JpegXlFile_SurfacesJxlStructureRow()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "image.jxl");
        using (var image = new MagickImage(MagickColors.SteelBlue, 16, 8) { Format = MagickFormat.Jxl })
            image.Write(path);

        var result = ImageMetadataService.Read(path);

        // A .jxl is either a bare codestream or an ISOBMFF container; either way the structure is reported.
        Assert.Contains(result.Rows, row => row.Label == "JPEG XL");
    }

    private static string FindValue(PhotoMetadata metadata, string label)
        => Assert.Single(metadata.Rows, row => row.Label == label).Value;
}

internal static class Exif31TestFixture
{
    private const ushort TypeAscii = 2;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;
    private const ushort TypeUndefined = 7;
    private const ushort TypeUtf8 = 129;

    public static ExifFixtureEntry Short(ushort tag, bool littleEndian, ushort value)
    {
        var data = new byte[2];
        WriteUInt16(data, value, littleEndian);
        return new ExifFixtureEntry(tag, TypeShort, 1, data);
    }

    public static ExifFixtureEntry Undefined(ushort tag, bool littleEndian, params ushort[] values)
    {
        var data = new byte[values.Length * 2];
        for (var index = 0; index < values.Length; index++)
            WriteUInt16(data.AsSpan(index * 2), values[index], littleEndian);
        return new ExifFixtureEntry(tag, TypeUndefined, (uint)data.Length, data);
    }

    public static ExifFixtureEntry Utf8(ushort tag, string value)
        => new(tag, TypeUtf8, (uint)Encoding.UTF8.GetByteCount(value) + 1, [.. Encoding.UTF8.GetBytes(value), 0]);

    public static void WriteJpeg(
        string path,
        bool littleEndian,
        string version,
        string make,
        params ExifFixtureEntry[] entries)
    {
        var profile = BuildProfile(littleEndian, version, make, entries);
        using var image = new MagickImage(MagickColors.CornflowerBlue, 8, 8)
        {
            Format = MagickFormat.Jpeg
        };
        var jpeg = image.ToByteArray(MagickFormat.Jpeg);
        Assert.True(jpeg.AsSpan().StartsWith(new byte[] { 0xff, 0xd8 }));
        Assert.True(profile.Length <= ushort.MaxValue - 2);

        var result = new byte[jpeg.Length + profile.Length + 4];
        jpeg.AsSpan(0, 2).CopyTo(result);
        result[2] = 0xff;
        result[3] = 0xe1;
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4), (ushort)(profile.Length + 2));
        profile.CopyTo(result.AsSpan(6));
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + profile.Length));
        File.WriteAllBytes(path, result);
    }

    private static byte[] BuildProfile(
        bool littleEndian,
        string version,
        string make,
        IReadOnlyList<ExifFixtureEntry> customEntries)
    {
        Assert.Equal(4, version.Length);
        var makeData = Encoding.ASCII.GetBytes(make + "\0");
        var exifEntries = new List<ExifFixtureEntry>
        {
            new(0x9000, TypeUndefined, 4, Encoding.ASCII.GetBytes(version))
        };
        exifEntries.AddRange(customEntries);
        exifEntries.Sort((left, right) => left.Tag.CompareTo(right.Tag));

        const int tiffHeaderSize = 8;
        const int primaryEntryCount = 2;
        const int primaryIfdOffset = tiffHeaderSize;
        const int primaryIfdSize = 2 + (primaryEntryCount * 12) + 4;
        var makeOffset = primaryIfdOffset + primaryIfdSize;
        var exifIfdOffset = Align(makeOffset + makeData.Length, 2);
        var exifIfdSize = 2 + (exifEntries.Count * 12) + 4;
        var externalOffset = exifIfdOffset + exifIfdSize;

        var externalOffsets = new int[exifEntries.Count];
        for (var index = 0; index < exifEntries.Count; index++)
        {
            if (exifEntries[index].Data.Length <= 4)
                continue;

            externalOffsets[index] = externalOffset;
            externalOffset = Align(externalOffset + exifEntries[index].Data.Length, 2);
        }

        var tiff = new byte[externalOffset];
        tiff[0] = littleEndian ? (byte)'I' : (byte)'M';
        tiff[1] = tiff[0];
        WriteUInt16(tiff.AsSpan(2), 42, littleEndian);
        WriteUInt32(tiff.AsSpan(4), primaryIfdOffset, littleEndian);

        WriteUInt16(tiff.AsSpan(primaryIfdOffset), primaryEntryCount, littleEndian);
        WriteEntry(
            tiff,
            primaryIfdOffset + 2,
            new ExifFixtureEntry(0x010f, TypeAscii, (uint)makeData.Length, makeData),
            makeOffset,
            littleEndian);
        WriteEntry(
            tiff,
            primaryIfdOffset + 14,
            new ExifFixtureEntry(0x8769, TypeLong, 1, UInt32Bytes((uint)exifIfdOffset, littleEndian)),
            0,
            littleEndian);
        makeData.CopyTo(tiff.AsSpan(makeOffset));

        WriteUInt16(tiff.AsSpan(exifIfdOffset), (ushort)exifEntries.Count, littleEndian);
        for (var index = 0; index < exifEntries.Count; index++)
        {
            WriteEntry(
                tiff,
                exifIfdOffset + 2 + (index * 12),
                exifEntries[index],
                externalOffsets[index],
                littleEndian);
            if (externalOffsets[index] != 0)
                exifEntries[index].Data.CopyTo(tiff.AsSpan(externalOffsets[index]));
        }

        var profile = new byte[tiff.Length + 6];
        "Exif\0\0"u8.CopyTo(profile);
        tiff.CopyTo(profile.AsSpan(6));
        return profile;
    }

    private static void WriteEntry(
        Span<byte> destination,
        int offset,
        ExifFixtureEntry entry,
        int externalOffset,
        bool littleEndian)
    {
        var target = destination[offset..];
        WriteUInt16(target, entry.Tag, littleEndian);
        WriteUInt16(target[2..], entry.Type, littleEndian);
        WriteUInt32(target[4..], entry.Count, littleEndian);
        if (entry.Data.Length <= 4)
            entry.Data.CopyTo(target[8..12]);
        else
            WriteUInt32(target[8..], (uint)externalOffset, littleEndian);
    }

    private static byte[] UInt32Bytes(uint value, bool littleEndian)
    {
        var bytes = new byte[4];
        WriteUInt32(bytes, value, littleEndian);
        return bytes;
    }

    private static int Align(int value, int alignment)
        => (value + alignment - 1) / alignment * alignment;

    private static void WriteUInt16(Span<byte> destination, ushort value, bool littleEndian)
    {
        if (littleEndian)
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
    }

    private static void WriteUInt32(Span<byte> destination, uint value, bool littleEndian)
    {
        if (littleEndian)
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
    }
}

internal sealed record ExifFixtureEntry(ushort Tag, ushort Type, uint Count, byte[] Data);
