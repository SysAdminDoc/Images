using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class EmailShareServiceTests
{
    [Fact]
    public void CreateDraftWithAttachmentCore_WritesUnsentEmlWithAttachment()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "photo.png");
        var draftFolder = Path.Combine(temp.Path, "drafts");
        File.WriteAllBytes(source, [1, 2, 3, 4, 5]);

        var result = EmailShareService.CreateDraftWithAttachmentCore(
            source,
            () => draftFolder,
            new DateTimeOffset(2026, 5, 14, 12, 30, 0, TimeSpan.Zero),
            () => Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal("photo.png", result.AttachmentFileName);
        Assert.Equal(Path.Combine(draftFolder, "images-email-20260514-123000-11111111111111111111111111111111.eml"), result.DraftPath);
        var draft = File.ReadAllText(result.DraftPath);
        Assert.Contains("X-Unsent: 1", draft);
        Assert.Contains("Subject: Image: photo.png", draft);
        Assert.Contains("Content-Disposition: attachment; filename=\"photo.png\"", draft);
        Assert.Contains(Convert.ToBase64String([1, 2, 3, 4, 5]), draft);
    }

    [Fact]
    public void CreateDraftWithAttachmentCore_EncodesNonAsciiSubjectAndFilename()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "café français.png");
        var draftFolder = Path.Combine(temp.Path, "drafts");
        File.WriteAllBytes(source, [9, 8, 7]);

        var result = EmailShareService.CreateDraftWithAttachmentCore(
            source,
            () => draftFolder,
            new DateTimeOffset(2026, 5, 14, 12, 30, 0, TimeSpan.Zero),
            () => Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var draft = File.ReadAllText(result.DraftPath);
        // Subject uses an RFC 2047 encoded word, not raw UTF-8 bytes.
        Assert.Contains("Subject: =?UTF-8?B?", draft);
        Assert.DoesNotContain("Subject: Image: café", draft, StringComparison.Ordinal);
        // Filename uses the RFC 2231 extended parameter.
        Assert.Contains("filename*=UTF-8''", draft);
        Assert.Contains("caf%C3%A9%20fran%C3%A7ais.png", draft);
        // Attachment bytes are still present and correct.
        Assert.Contains(Convert.ToBase64String([9, 8, 7]), draft);
    }

    [Fact]
    public void CreateDraftWithAttachmentCore_WhenSourceMissingThrows()
    {
        using var temp = TestDirectory.Create();

        Assert.Throws<FileNotFoundException>(() => EmailShareService.CreateDraftWithAttachmentCore(
            Path.Combine(temp.Path, "missing.png"),
            () => temp.Path,
            DateTimeOffset.Now,
            Guid.NewGuid));
    }
}
