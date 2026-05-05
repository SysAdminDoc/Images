using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ClipboardImportServiceTests
{
    [Fact]
    public void Import_FileDropList_OpensFirstSupportedExistingFile()
    {
        using var temp = TestDirectory.Create();
        var text = temp.WriteFile("notes.txt");
        var image = temp.WriteFile("photo.jpg");
        var missing = Path.Combine(temp.Path, "missing.png");
        var source = new FakeClipboardDataSource
        {
            FileDropList = [text, missing, image]
        };
        var service = CreateService(source, temp.Path);

        var result = service.Import();

        Assert.Equal(ClipboardImportStatus.OpenExistingFile, result.Status);
        Assert.Equal(image, result.Path);
        Assert.Equal("", result.Message);
    }

    [Fact]
    public void Import_FileDropListWithoutSupportedImage_ReturnsActionableMessage()
    {
        using var temp = TestDirectory.Create();
        var text = temp.WriteFile("notes.txt");
        var source = new FakeClipboardDataSource
        {
            FileDropList = [text, Path.Combine(temp.Path, "missing.jpg")]
        };
        var service = CreateService(source, temp.Path);

        var result = service.Import();

        Assert.Equal(ClipboardImportStatus.NoSupportedFile, result.Status);
        Assert.Null(result.Path);
        Assert.Equal("No supported image in the clipboard file list", result.Message);
    }

    [Fact]
    public void Import_ImageData_SavesCollisionResistantPng()
    {
        using var temp = TestDirectory.Create();
        var source = new FakeClipboardDataSource
        {
            Image = CreateBitmap()
        };
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var service = CreateService(
            source,
            temp.Path,
            () => new DateTimeOffset(2026, 5, 5, 12, 34, 56, 789, TimeSpan.Zero),
            () => guid);

        var result = service.Import();

        Assert.Equal(ClipboardImportStatus.OpenSavedImage, result.Status);
        Assert.Equal(Path.Combine(temp.Path, "clipboard-20260505-123456789-00112233445566778899aabbccddeeff.png"), result.Path);
        Assert.True(File.Exists(result.Path));
        Assert.True(new FileInfo(result.Path).Length > 0);
        Assert.Equal("Pasted from clipboard", result.Message);
    }

    [Fact]
    public void Import_ImageDataWithoutStorage_ReturnsStorageMessage()
    {
        var source = new FakeClipboardDataSource
        {
            Image = CreateBitmap()
        };
        var service = new ClipboardImportService(
            source,
            () => null,
            () => DateTimeOffset.UnixEpoch,
            Guid.NewGuid);

        var result = service.Import();

        Assert.Equal(ClipboardImportStatus.StorageUnavailable, result.Status);
        Assert.Null(result.Path);
        Assert.Equal("Paste failed: could not create temp folder", result.Message);
    }

    [Fact]
    public void Import_EmptyClipboard_ReturnsNothingImageLikeMessage()
    {
        using var temp = TestDirectory.Create();
        var service = CreateService(new FakeClipboardDataSource(), temp.Path);

        var result = service.Import();

        Assert.Equal(ClipboardImportStatus.NothingImageLike, result.Status);
        Assert.Null(result.Path);
        Assert.Equal("Nothing image-like in the clipboard", result.Message);
    }

    [Fact]
    public void PruneClipboardImages_RemovesOldExcessAndOversizedFiles()
    {
        using var temp = TestDirectory.Create();
        var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        var newest = WriteClipboardFile(temp.Path, "clipboard-newest.png", 40, now.AddMinutes(-1));
        var second = WriteClipboardFile(temp.Path, "clipboard-second.png", 40, now.AddMinutes(-2));
        var third = WriteClipboardFile(temp.Path, "clipboard-third.png", 40, now.AddMinutes(-3));
        var old = WriteClipboardFile(temp.Path, "clipboard-old.png", 40, now.AddDays(-10));
        var ignored = WriteClipboardFile(temp.Path, "not-clipboard.png", 40, now.AddDays(-10));

        var deleted = ClipboardImportService.PruneClipboardImages(
            temp.Path,
            new ClipboardPruneOptions(MaxCount: 3, MaxBytes: 90, MaxAge: TimeSpan.FromDays(7)),
            now);

        Assert.Equal(2, deleted);
        Assert.True(File.Exists(newest));
        Assert.True(File.Exists(second));
        Assert.False(File.Exists(third));
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(ignored));
    }

    private static ClipboardImportService CreateService(
        FakeClipboardDataSource source,
        string clipboardDirectory,
        Func<DateTimeOffset>? getUtcNow = null,
        Func<Guid>? newGuid = null)
    {
        return new ClipboardImportService(
            source,
            () => clipboardDirectory,
            getUtcNow ?? (() => DateTimeOffset.UnixEpoch),
            newGuid ?? (() => Guid.Empty));
    }

    private static BitmapSource CreateBitmap()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0xFF, 0x00, 0x00, 0xFF },
            4);
        bitmap.Freeze();
        return bitmap;
    }

    private static string WriteClipboardFile(string folder, string name, int bytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)0x42, bytes).ToArray());
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    private sealed class FakeClipboardDataSource : IClipboardDataSource
    {
        public IReadOnlyList<string>? FileDropList { get; init; }
        public BitmapSource? Image { get; init; }

        public bool ContainsFileDropList() => FileDropList is not null;

        public IReadOnlyList<string> GetFileDropList() => FileDropList ?? [];

        public bool ContainsImage() => Image is not null;

        public BitmapSource? GetImage() => Image;
    }
}
