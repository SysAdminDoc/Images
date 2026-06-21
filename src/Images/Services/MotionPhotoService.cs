using System.IO;
using System.Text;

namespace Images.Services;

public sealed record MotionPhotoInfo(long VideoOffset, long VideoLength, string ContainerType);

/// <summary>
/// Detects and extracts embedded video segments from Motion Photos (Samsung/Google Pixel)
/// and Apple Live Photos (.mov companion files).
/// Samsung embeds MP4 at the end of JPEG files with an XMP MotionPhoto marker.
/// Google Pixel uses a similar pattern with an "ftypmp4" / "ftypisom" box.
/// Apple stores the video as a separate .mov file alongside the JPEG.
/// </summary>
public static class MotionPhotoService
{
    private static readonly byte[] FtypSignature = "ftyp"u8.ToArray();
    private static readonly byte[] Mp4Signatures = "ftypisom"u8.ToArray();
    private static readonly byte[] Mp41Signatures = "ftypmp41"u8.ToArray();
    private static readonly byte[] Mp42Signatures = "ftypmp42"u8.ToArray();
    private static readonly byte[] FtypMp4 = "ftypmp4"u8.ToArray();

    private static readonly HashSet<string> JpegExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif", ".heic", ".heif"
    };

    public static bool IsMotionPhotoCandidate(string path)
        => JpegExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Scans a JPEG/HEIC file for an embedded MP4 video segment.
    /// Returns null if no embedded video is found.
    /// </summary>
    public static MotionPhotoInfo? Detect(string path)
    {
        if (!IsMotionPhotoCandidate(path))
            return null;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < 16)
                return null;

            return FindEmbeddedMp4(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether a companion .mov file exists for the given image (Apple Live Photo pattern).
    /// </summary>
    public static string? FindCompanionVideo(string imagePath)
    {
        var dir = Path.GetDirectoryName(imagePath);
        var stem = Path.GetFileNameWithoutExtension(imagePath);
        if (dir is null || stem is null) return null;

        string[] videoExtensions = [".mov", ".MOV", ".mp4", ".MP4"];
        foreach (var ext in videoExtensions)
        {
            var candidate = Path.Combine(dir, stem + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Extracts the embedded video segment to a file. Returns the output path, or null on failure.
    /// </summary>
    public static string? ExtractEmbeddedVideo(string imagePath, MotionPhotoInfo info, string? outputDir = null)
    {
        try
        {
            var dir = outputDir ?? Path.GetDirectoryName(imagePath) ?? Path.GetTempPath();
            var stem = Path.GetFileNameWithoutExtension(imagePath);
            var outputPath = Path.Combine(dir, $"{stem}_motion.mp4");

            var suffix = 2;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(dir, $"{stem}_motion ({suffix}).mp4");
                suffix++;
            }

            using var input = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            input.Seek(info.VideoOffset, SeekOrigin.Begin);

            using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
            var remaining = info.VideoLength;
            var buffer = new byte[81920];
            while (remaining > 0)
            {
                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = input.Read(buffer, 0, toRead);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                remaining -= read;
            }

            if (remaining > 0)
            {
                try { File.Delete(outputPath); } catch { }
                return null;
            }

            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Searches backward from the end of the stream for an MP4 ftyp box.
    /// </summary>
    private static MotionPhotoInfo? FindEmbeddedMp4(FileStream stream)
    {
        const int scanSize = 256 * 1024;
        var fileLength = stream.Length;
        if (fileLength < 12)
            return null;

        var bufferSize = (int)Math.Min(scanSize, fileLength);
        var buffer = new byte[bufferSize];
        var startPos = Math.Max(0, fileLength - bufferSize);

        stream.Seek(startPos, SeekOrigin.Begin);
        var bytesRead = stream.Read(buffer, 0, bufferSize);
        if (bytesRead < 12)
            return null;

        for (var i = 0; i < bytesRead - 8; i++)
        {
            if (buffer[i + 4] == FtypSignature[0] &&
                buffer[i + 5] == FtypSignature[1] &&
                buffer[i + 6] == FtypSignature[2] &&
                buffer[i + 7] == FtypSignature[3])
            {
                var boxSize = (buffer[i] << 24) | (buffer[i + 1] << 16) | (buffer[i + 2] << 8) | buffer[i + 3];
                if (boxSize < 8 || boxSize > 64) continue;

                var absoluteOffset = startPos + i;
                if (absoluteOffset <= 2) continue;

                var brand = Encoding.ASCII.GetString(buffer, i + 8, Math.Min(4, bytesRead - i - 8));
                var videoLength = fileLength - absoluteOffset;

                return new MotionPhotoInfo(absoluteOffset, videoLength, brand.TrimEnd('\0'));
            }
        }

        return null;
    }
}
