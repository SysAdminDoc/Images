using System.Buffers;
using System.IO;
using System.Text;

namespace Images.Services;

/// <summary>
/// Reads user-selected text files without trusting a stale length check or allocating from an
/// unbounded stream. Files are opened with replacement-friendly sharing like image readers.
/// </summary>
public static class BoundedTextFileReader
{
    public const int MaxWorkflowImportBytes = 1024 * 1024;
    public const int MaxServiceMetadataBytes = 1024 * 1024;
    public const int MaxServiceStateBytes = 16 * 1024 * 1024;

    public static string ReadUtf8(string path, int maxBytes, string contentLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (input.Length > maxBytes)
            throw TooLarge(contentLabel, maxBytes);

        using var bytes = new MemoryStream(capacity: (int)Math.Min(input.Length, maxBytes));
        var bufferSize = (int)Math.Min(81920L, (long)maxBytes + 1);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (true)
            {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                if (bytes.Length > maxBytes - read)
                    throw TooLarge(contentLabel, maxBytes);
                bytes.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        bytes.Position = 0;
        using var reader = new StreamReader(
            bytes,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static InvalidDataException TooLarge(string contentLabel, int maxBytes)
    {
        var label = string.IsNullOrWhiteSpace(contentLabel) ? "Text file" : contentLabel.Trim();
        return new InvalidDataException($"{label} exceeds the {maxBytes / 1024:N0} KiB import limit.");
    }
}
