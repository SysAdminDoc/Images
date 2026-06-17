using System.IO;
using System.Text;

namespace Images.Services;

public sealed record FormatSignature(
    string FormatName,
    string SuggestedExtension,
    IReadOnlyList<string> AcceptedExtensions)
{
    public bool MatchesExtension(string extension)
        => AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
}

public static class FormatSignatureDetector
{
    public static readonly FormatSignature Png = new("PNG", ".png", [".png", ".apng"]);
    public static readonly FormatSignature Jpeg = new("JPEG", ".jpg", [".jpg", ".jpeg", ".jpe", ".jfif", ".jif"]);
    public static readonly FormatSignature Gif = new("GIF", ".gif", [".gif"]);
    public static readonly FormatSignature WebP = new("WebP", ".webp", [".webp"]);
    public static readonly FormatSignature Tiff = new("TIFF", ".tif", [".tif", ".tiff"]);
    public static readonly FormatSignature Bmp = new("BMP", ".bmp", [".bmp", ".dib"]);
    public static readonly FormatSignature Ico = new("ICO", ".ico", [".ico"]);
    public static readonly FormatSignature Pdf = new("PDF", ".pdf", [".pdf", ".pdfa"]);
    public static readonly FormatSignature Psd = new("Photoshop", ".psd", [".psd"]);
    public static readonly FormatSignature Qoi = new("QOI", ".qoi", [".qoi"]);
    public static readonly FormatSignature Jxl = new("JPEG XL", ".jxl", [".jxl"]);
    public static readonly FormatSignature Avif = new("AVIF", ".avif", [".avif"]);
    public static readonly FormatSignature Heic = new("HEIC/HEIF", ".heic", [".heic", ".heif", ".hif"]);

    public static FormatSignature? Detect(string path)
    {
        Span<byte> buffer = stackalloc byte[32];
        int read;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            read = stream.Read(buffer);
        }
        catch
        {
            return null;
        }

        return DetectFromBytes(buffer[..read]);
    }

    public static FormatSignature? DetectFromBytes(ReadOnlySpan<byte> bytes)
    {
        if (StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return Png;
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return Jpeg;
        if (StartsWithAscii(bytes, "GIF87a") || StartsWithAscii(bytes, "GIF89a"))
            return Gif;
        if (bytes.Length >= 12 && StartsWithAscii(bytes, "RIFF") && AsciiEquals(bytes[8..12], "WEBP"))
            return WebP;
        if (StartsWith(bytes, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(bytes, [0x4D, 0x4D, 0x00, 0x2A]))
            return Tiff;
        if (StartsWithAscii(bytes, "BM"))
            return Bmp;
        if (StartsWith(bytes, [0x00, 0x00, 0x01, 0x00]))
            return Ico;
        if (StartsWithAscii(bytes, "%PDF"))
            return Pdf;
        if (StartsWithAscii(bytes, "8BPS"))
            return Psd;
        if (StartsWithAscii(bytes, "qoif"))
            return Qoi;
        if (StartsWith(bytes, [0xFF, 0x0A]) || (bytes.Length >= 12 && StartsWith(bytes, [0x00, 0x00, 0x00, 0x0C]) && AsciiEquals(bytes[4..12], "JXL \r\n\x87\n")))
            return Jxl;
        if (bytes.Length >= 12 && AsciiEquals(bytes[4..8], "ftyp"))
        {
            var brand = Encoding.ASCII.GetString(bytes[8..12]);
            if (brand.StartsWith("avif", StringComparison.OrdinalIgnoreCase) || brand.StartsWith("avis", StringComparison.OrdinalIgnoreCase))
                return Avif;
            if (brand.StartsWith("heic", StringComparison.OrdinalIgnoreCase) ||
                brand.StartsWith("heix", StringComparison.OrdinalIgnoreCase) ||
                brand.StartsWith("hevc", StringComparison.OrdinalIgnoreCase) ||
                brand.StartsWith("mif1", StringComparison.OrdinalIgnoreCase))
                return Heic;
        }

        return null;
    }

    public static bool HasMismatch(string path)
    {
        var signature = Detect(path);
        return signature is not null && !signature.MatchesExtension(Path.GetExtension(path));
    }

    public static (FormatSignature Signature, string FileExtension)? DetectMismatch(string path)
    {
        var signature = Detect(path);
        if (signature is null) return null;
        var ext = Path.GetExtension(path);
        return signature.MatchesExtension(ext) ? null : (signature, ext);
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix)
        => bytes.Length >= prefix.Length && bytes[..prefix.Length].SequenceEqual(prefix);

    private static bool StartsWithAscii(ReadOnlySpan<byte> bytes, string text)
        => bytes.Length >= text.Length && AsciiEquals(bytes[..text.Length], text);

    private static bool AsciiEquals(ReadOnlySpan<byte> bytes, string text)
    {
        if (bytes.Length != text.Length)
            return false;

        for (var i = 0; i < text.Length; i++)
        {
            if (bytes[i] != (byte)text[i])
                return false;
        }

        return true;
    }
}
