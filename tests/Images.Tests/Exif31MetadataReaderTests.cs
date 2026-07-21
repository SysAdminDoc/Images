using Images.Services;

namespace Images.Tests;

public sealed class Exif31MetadataReaderTests
{
    // Minimal big-endian ("MM") TIFF/Exif profile: IFD0 -> Exif IFD with ExifVersion "0300"
    // plus a DistortionCorrection short. Layout is documented inline in BuildProfile.
    [Fact]
    public void Read_ParsesExif31CorrectionWhenVersionAndShapeAreValid()
    {
        var profile = BuildProfile("0300"u8.ToArray(), distortion: 1);

        var metadata = Exif31MetadataReader.Read(profile);

        Assert.NotNull(metadata);
        Assert.Equal((ushort)1, metadata!.DistortionCorrection);
    }

    [Fact]
    public void Read_RejectsIncompatibleExifVersion()
    {
        var profile = BuildProfile("0200"u8.ToArray(), distortion: 1);

        Assert.Null(Exif31MetadataReader.Read(profile));
    }

    [Fact]
    public void Read_RejectsProfileWithNoExif31Signature()
    {
        // Valid "0300" version but no learning/development/description/correction field.
        var profile = BuildProfile("0300"u8.ToArray(), distortion: null);

        Assert.Null(Exif31MetadataReader.Read(profile));
    }

    [Fact]
    public void Read_ReturnsNullForTruncatedProfile()
    {
        Assert.Null(Exif31MetadataReader.Read(new byte[8]));
        Assert.Null(Exif31MetadataReader.Read((byte[]?)null));
    }

    private static byte[] BuildProfile(byte[] exifVersion, ushort? distortion)
    {
        // 56-byte big-endian TIFF.
        var bytes = new byte[56];
        // TIFF header.
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'M';
        WriteUInt16(bytes, 2, 42);
        WriteUInt32(bytes, 4, 8);            // first IFD at offset 8

        // IFD0: one entry (Exif IFD pointer).
        WriteUInt16(bytes, 8, 1);            // entry count
        WriteUInt16(bytes, 10, 0x8769);      // ExifIFDPointer tag
        WriteUInt16(bytes, 12, 4);           // type LONG
        WriteUInt32(bytes, 14, 1);           // count
        WriteUInt32(bytes, 18, 26);          // Exif IFD at offset 26
        WriteUInt32(bytes, 22, 0);           // next IFD offset

        // Exif IFD: ExifVersion + DistortionCorrection.
        WriteUInt16(bytes, 26, 2);           // entry count
        WriteUInt16(bytes, 28, 0x9000);      // ExifVersion tag
        WriteUInt16(bytes, 30, 7);           // type UNDEFINED
        WriteUInt32(bytes, 32, 4);           // count
        bytes[36] = exifVersion[0];
        bytes[37] = exifVersion[1];
        bytes[38] = exifVersion[2];
        bytes[39] = exifVersion[3];
        WriteUInt16(bytes, 40, 0xA40F);      // DistortionCorrection tag
        WriteUInt16(bytes, 42, 3);           // type SHORT
        WriteUInt32(bytes, 44, 1);           // count
        WriteUInt16(bytes, 48, distortion ?? 0); // inline value (0 = a benign short if suppressed)
        WriteUInt32(bytes, 52, 0);           // next IFD offset

        if (distortion is null)
        {
            // Replace the correction entry with an unrelated tag the reader ignores (ColorSpace),
            // so no Exif 3.1 signature field is present while the profile stays structurally valid.
            WriteUInt16(bytes, 40, 0xA001);  // ColorSpace — not an Exif 3.1 signature tag
            WriteUInt16(bytes, 42, 3);       // type SHORT
            WriteUInt32(bytes, 44, 1);       // count
            WriteUInt16(bytes, 48, 1);       // inline value
        }

        return bytes;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
