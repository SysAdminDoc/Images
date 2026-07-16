using System.IO;

namespace Images.Services;

/// <summary>
/// Read-only structural inspection of JPEG XL files. Distinguishes a bare codestream from an
/// ISOBMFF container and, within a container, detects the JPEG bitstream reconstruction data
/// (<c>jbrd</c>) box that marks a lossless JPEG-to-JXL transcode — a headline JXL capability no
/// mainstream Windows viewer surfaces. Header-only; it never fully decodes the image.
/// </summary>
public static class JxlInspectionService
{
    // The signature and the reconstruction box both live in the file header; a small window is
    // enough and keeps this cheap to run for every details-panel read.
    private const int HeaderScanBytes = 64 * 1024;

    // Bare JXL codestream marker.
    private static readonly byte[] CodestreamSignature = [0xFF, 0x0A];

    // ISOBMFF JXL signature box: size(12) + "JXL " + 0D 0A 87 0A.
    private static readonly byte[] ContainerSignature =
        [0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A];

    // ISOBMFF box type for JPEG bitstream reconstruction data.
    private static readonly byte[] ReconstructionBox = [0x6A, 0x62, 0x72, 0x64]; // "jbrd"

    public static JxlInspection Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return JxlInspection.NotJxl;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = (int)Math.Min(stream.Length, HeaderScanBytes);
            var buffer = new byte[length];
            var read = ReadFully(stream, buffer);
            return Inspect(buffer.AsSpan(0, read));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return JxlInspection.NotJxl;
        }
    }

    /// <summary>Inspects an already-loaded header window. Exposed for testing.</summary>
    public static JxlInspection Inspect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(ContainerSignature))
        {
            var isTranscode = header.IndexOf(ReconstructionBox) >= 0;
            return new JxlInspection(
                true,
                isTranscode ? JxlContainerKind.JpegTranscode : JxlContainerKind.Container);
        }

        if (header.StartsWith(CodestreamSignature))
            return new JxlInspection(true, JxlContainerKind.Codestream);

        return JxlInspection.NotJxl;
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }

        return total;
    }
}

public enum JxlContainerKind
{
    None,
    Codestream,
    Container,
    JpegTranscode,
}

public sealed record JxlInspection(bool IsJxl, JxlContainerKind Kind)
{
    public static JxlInspection NotJxl { get; } = new(false, JxlContainerKind.None);
}
