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
        writer.WriteLine("Subject: " + EscapeHeader("Image: " + fileName));
        writer.WriteLine("MIME-Version: 1.0");
        writer.WriteLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
        writer.WriteLine();
        writer.WriteLine($"--{boundary}");
        writer.WriteLine("Content-Type: text/plain; charset=utf-8");
        writer.WriteLine("Content-Transfer-Encoding: quoted-printable");
        writer.WriteLine();
        writer.WriteLine("Attached from Images.");
        writer.WriteLine("Source path: " + sourcePath);
        writer.WriteLine();
        writer.WriteLine($"--{boundary}");
        writer.WriteLine($"Content-Type: application/octet-stream; name=\"{EscapeQuoted(fileName)}\"");
        writer.WriteLine("Content-Transfer-Encoding: base64");
        writer.WriteLine($"Content-Disposition: attachment; filename=\"{EscapeQuoted(fileName)}\"");
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
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            var encoded = Convert.ToBase64String(buffer, 0, read);
            for (var offset = 0; offset < encoded.Length; offset += MaxBase64LineLength)
                writer.WriteLine(encoded.Substring(offset, Math.Min(MaxBase64LineLength, encoded.Length - offset)));
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

    private static string EscapeHeader(string value)
        => value.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);

    private static string EscapeQuoted(string value)
        => EscapeHeader(value).Replace("\"", "'", StringComparison.Ordinal);
}
