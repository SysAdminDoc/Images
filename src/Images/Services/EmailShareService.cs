using System.Globalization;
using System.IO;
using System.Text;

namespace Images.Services;

public sealed record EmailDraftResult(string DraftPath, string AttachmentFileName);

public static class EmailShareService
{
    private const int MaxBase64LineLength = 76;
    private const int BinaryReadChunkSize = 57 * 1024;

    public static EmailDraftResult CreateDraftWithAttachment(string imagePath)
        => CreateDraftWithAttachmentCore(
            imagePath,
            () => AppStorage.TryGetAppDirectory("email-drafts") ?? Path.Combine(Path.GetTempPath(), "Images", "email-drafts"),
            DateTimeOffset.Now,
            Guid.NewGuid);

    internal static EmailDraftResult CreateDraftWithAttachmentCore(
        string imagePath,
        Func<string?> getDraftDirectory,
        DateTimeOffset now,
        Func<Guid> newGuid)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("An image path is required.", nameof(imagePath));

        var sourcePath = Path.GetFullPath(imagePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Image file not found.", sourcePath);

        var draftDirectory = getDraftDirectory()
            ?? throw new InvalidOperationException("Could not create an email draft folder.");
        Directory.CreateDirectory(draftDirectory);

        PruneOldDrafts(draftDirectory, now);

        var fileName = Path.GetFileName(sourcePath);
        var draftName = $"images-email-{now:yyyyMMdd-HHmmss}-{newGuid():N}.eml";
        var draftPath = Path.Combine(draftDirectory, draftName);
        var boundary = "----=_Images_" + newGuid().ToString("N");

        using var writer = new StreamWriter(draftPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.NewLine = "\r\n";
        writer.WriteLine("X-Unsent: 1");
        writer.WriteLine("To:");
        writer.WriteLine("Subject: " + EncodeHeaderValue("Image: " + fileName));
        writer.WriteLine("MIME-Version: 1.0");
        writer.WriteLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
        writer.WriteLine();
        writer.WriteLine($"--{boundary}");
        writer.WriteLine("Content-Type: text/plain; charset=utf-8");
        writer.WriteLine("Content-Transfer-Encoding: quoted-printable");
        writer.WriteLine();
        writer.WriteLine(EncodeQuotedPrintable("Attached from Images."));
        writer.WriteLine(EncodeQuotedPrintable("Source path: " + sourcePath));
        writer.WriteLine();
        writer.WriteLine($"--{boundary}");
        writer.WriteLine($"Content-Type: application/octet-stream; {EncodeFileNameParameter("name", fileName)}");
        writer.WriteLine("Content-Transfer-Encoding: base64");
        writer.WriteLine($"Content-Disposition: attachment; {EncodeFileNameParameter("filename", fileName)}");
        writer.WriteLine();
        WriteAttachmentBase64(sourcePath, writer);
        writer.WriteLine();
        writer.WriteLine($"--{boundary}--");

        return new EmailDraftResult(draftPath, fileName);
    }

    private static void WriteAttachmentBase64(string sourcePath, TextWriter writer)
    {
        using var stream = File.OpenRead(sourcePath);
        var buffer = new byte[BinaryReadChunkSize];
        int read;
        // ReadAtLeast fills the buffer across short reads (permitted by the
        // Stream contract on network/SMB files); encoding a partial buffer
        // whose length isn't a multiple of 3 would emit '=' padding mid-stream
        // and corrupt the attachment.
        while ((read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)) > 0)
        {
            var encoded = Convert.ToBase64String(buffer, 0, read);
            for (var offset = 0; offset < encoded.Length; offset += MaxBase64LineLength)
                writer.WriteLine(encoded.Substring(offset, Math.Min(MaxBase64LineLength, encoded.Length - offset)));

            if (read < buffer.Length)
                break;
        }
    }

    private static void PruneOldDrafts(string draftDirectory, DateTimeOffset now)
    {
        try
        {
            var cutoff = now - TimeSpan.FromDays(7);
            foreach (var file in Directory.EnumerateFiles(draftDirectory, "images-email-*.eml"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff.UtcDateTime)
                    info.Delete();
            }
        }
        catch
        {
            // Draft cleanup is opportunistic; failure should not block creating the requested email.
        }
    }

    private static string StripCrlf(string value)
        => value.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);

    private static bool IsAscii(string value)
    {
        foreach (var c in value)
        {
            if (c > '\x7F')
                return false;
        }

        return true;
    }

    /// <summary>
    /// RFC 2047 encoded-word for a header value. ASCII values pass through so
    /// clients render them verbatim; non-ASCII values are B-encoded in UTF-8,
    /// chunked so each encoded word stays within the 75-char limit and folded
    /// with continuation whitespace.
    /// </summary>
    private static string EncodeHeaderValue(string value)
    {
        value = StripCrlf(value);
        if (IsAscii(value))
            return value;

        var sb = new StringBuilder();
        var chunk = new List<byte>(48);

        void FlushChunk()
        {
            if (chunk.Count == 0)
                return;
            if (sb.Length > 0)
                sb.Append("\r\n "); // folding whitespace between encoded words
            sb.Append("=?UTF-8?B?").Append(Convert.ToBase64String(chunk.ToArray())).Append("?=");
            chunk.Clear();
        }

        Span<byte> runeBytes = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            var written = rune.EncodeToUtf8(runeBytes);
            // Keep the base64 payload (chunk*4/3) under ~63 chars so the whole
            // encoded word fits in 75; never split a UTF-8 rune across words.
            if (chunk.Count + written > 45)
                FlushChunk();
            for (var i = 0; i < written; i++)
                chunk.Add(runeBytes[i]);
        }

        FlushChunk();
        return sb.ToString();
    }

    /// <summary>
    /// Emits a MIME parameter (name=/filename=). ASCII values use a normal
    /// quoted-string; non-ASCII values use the RFC 2231 extended syntax
    /// (<c>name*=UTF-8''pct-encoded</c>) so mail clients decode them correctly.
    /// </summary>
    private static string EncodeFileNameParameter(string parameter, string value)
    {
        value = StripCrlf(value);
        if (IsAscii(value) && !value.Contains('"', StringComparison.Ordinal))
            return $"{parameter}=\"{value}\"";

        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            // RFC 2231 attribute-char set: keep unreserved token chars literal.
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') ||
                b is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~')
                sb.Append((char)b);
            else
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return $"{parameter}*=UTF-8''{sb}";
    }

    /// <summary>
    /// Quoted-printable encoding for a text-body line, with soft line breaks so
    /// no line exceeds 76 characters (RFC 2045). The body declares
    /// quoted-printable, so non-ASCII source paths must actually be encoded.
    /// </summary>
    private static string EncodeQuotedPrintable(string text)
    {
        text = StripCrlf(text);
        var sb = new StringBuilder();
        var lineLength = 0;

        void Emit(string token)
        {
            if (lineLength + token.Length > 75)
            {
                sb.Append("=\r\n");
                lineLength = 0;
            }

            sb.Append(token);
            lineLength += token.Length;
        }

        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            if (b is >= 33 and <= 126 && b != (byte)'=')
                Emit(((char)b).ToString());
            else if (b is (byte)' ' or (byte)'\t')
                Emit(((char)b).ToString());
            else
                Emit("=" + b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
