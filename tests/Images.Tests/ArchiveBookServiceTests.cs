using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;

namespace Images.Tests;

public sealed class ArchiveBookServiceTests
{
    [Fact]
    public void LoadPage_WithGeneratedCbz_NaturalSortsPages()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("page10.png", 0xFF, 0x00, 0x00),
            ("page2.png", 0x00, 0xFF, 0x00),
            ("page1.png", 0x00, 0x00, 0xFF));

        var names = ArchiveBookService.ListPageNames(archivePath);
        var second = ArchiveBookService.LoadPage(archivePath, requestedPageIndex: 1);

        Assert.Equal(["page1.png", "page2.png", "page10.png"], names);
        Assert.Equal("page2.png", second.EntryName);
        Assert.Equal(1, second.PageIndex);
        Assert.Equal(3, second.PageCount);
        Assert.False(second.IsCover);
        Assert.NotEmpty(second.Bytes);
    }

    [Fact]
    public void LoadPage_WithGeneratedCb7_UsesManagedArchiveAdapterAndNaturalSortsPages()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cb7");
        WriteSevenZipArchive(
            archivePath,
            ("page10.png", 0xFF, 0x00, 0x00),
            ("page2.png", 0x00, 0xFF, 0x00),
            ("page1.png", 0x00, 0x00, 0xFF));

        var names = ArchiveBookService.ListPageNames(archivePath);
        var second = ArchiveBookService.LoadPage(archivePath, requestedPageIndex: 1);

        Assert.Equal(["page1.png", "page2.png", "page10.png"], names);
        Assert.Equal("page2.png", second.EntryName);
        Assert.Equal(1, second.PageIndex);
        Assert.Equal(3, second.PageCount);
        Assert.NotEmpty(second.Bytes);
    }

    [Theory]
    [InlineData(".rar")]
    [InlineData(".cbr")]
    [InlineData(".7z")]
    [InlineData(".cb7")]
    public void SupportedArchiveExtensions_IncludeManagedArchiveBooks(string extension)
    {
        Assert.True(SupportedImageFormats.IsSupported($"book{extension}"));
        Assert.True(SupportedImageFormats.IsArchive($"book{extension}"));
    }

    [Fact]
    public void LoadPage_WhenArchiveHasExplicitCover_PromotesCoverBeforeNaturalPages()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("pages/page1.png", 0xFF, 0x00, 0x00),
            ("bonus/cover.png", 0x00, 0xFF, 0x00),
            ("pages/page2.png", 0x00, 0x00, 0xFF));

        var names = ArchiveBookService.ListPageNames(archivePath);
        var cover = ArchiveBookService.LoadPage(archivePath, requestedPageIndex: 0);

        Assert.Equal(["bonus/cover.png", "pages/page1.png", "pages/page2.png"], names);
        Assert.Equal("bonus/cover.png", cover.EntryName);
        Assert.True(cover.IsCover);
        Assert.Equal(0, cover.PageIndex);
        Assert.Equal(3, cover.PageCount);
    }

    [Fact]
    public void LoadSpread_KeepsExplicitCoverSingleThenPairsNaturalPages()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("pages/page1.png", 0xFF, 0x00, 0x00),
            ("pages/page2.png", 0x00, 0xFF, 0x00),
            ("cover.png", 0x00, 0x00, 0xFF),
            ("pages/page3.png", 0xFF, 0xFF, 0x00));

        var cover = ArchiveBookService.LoadSpread(archivePath, requestedPageIndex: 0);
        var middle = ArchiveBookService.LoadSpread(archivePath, requestedPageIndex: 2);
        var last = ArchiveBookService.LoadSpread(archivePath, requestedPageIndex: 3);

        Assert.False(cover.IsSpread);
        Assert.Equal(0, cover.PageIndex);
        Assert.Equal(["cover.png"], cover.Pages.Select(page => page.EntryName));

        Assert.True(middle.IsSpread);
        Assert.Equal(1, middle.PageIndex);
        Assert.Equal(["pages/page1.png", "pages/page2.png"], middle.Pages.Select(page => page.EntryName));

        Assert.False(last.IsSpread);
        Assert.Equal(3, last.PageIndex);
        Assert.Equal(["pages/page3.png"], last.Pages.Select(page => page.EntryName));
    }

    [Fact]
    public void ListPageNames_SkipsUnsafeUnsupportedAndRecursiveEntries()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "mixed.zip");

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            WritePngEntry(archive, "../evil.png", 0xFF, 0x00, 0x00);
            WritePngEntry(archive, "safe/page1.png", 0x00, 0xFF, 0x00);
            WriteBytesEntry(archive, "safe/inner.cbz", [0x50, 0x4B, 0x03, 0x04]);
            WriteBytesEntry(archive, "notes.txt", "not an image"u8.ToArray());
        }

        var names = ArchiveBookService.ListPageNames(archivePath);

        Assert.Equal(["safe/page1.png"], names);
    }

    [Fact]
    public void ListPageNames_WithGenerated7z_SkipsUnsupportedDocumentsAndRecursiveEntries()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "mixed.7z");

        using (var output = File.Create(archivePath))
        using (var writer = WriterFactory.OpenWriter(
                   output,
                   ArchiveType.SevenZip,
                   new SevenZipWriterOptions(CompressionType.LZMA)))
        {
            WritePngEntry(writer, "safe/page1.png", 0x00, 0xFF, 0x00);
            WriteBytesEntry(writer, "safe/inner.cb7", [0x37, 0x7A, 0xBC, 0xAF]);
            WriteBytesEntry(writer, "preview.pdf", "%PDF-1.7"u8.ToArray());
            WriteBytesEntry(writer, "notes.txt", "not an image"u8.ToArray());
        }

        var names = ArchiveBookService.ListPageNames(archivePath);

        Assert.Equal(["safe/page1.png"], names);
    }

    [Fact]
    public void ListPageNames_WhenManagedArchiveIsCorrupt_ReturnsEmptyList()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "broken.7z");
        File.WriteAllBytes(archivePath, [0x37, 0x7A, 0xBC, 0xAF]);

        var names = ArchiveBookService.ListPageNames(archivePath);

        Assert.Empty(names);
    }

    [Fact]
    public void LoadPage_WhenArchiveHasNoSupportedPages_ThrowsActionableError()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "empty.cbz");

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            WriteBytesEntry(archive, "notes.txt", "not an image"u8.ToArray());

        var ex = Assert.Throws<InvalidOperationException>(() => ArchiveBookService.LoadPage(archivePath, 0));

        Assert.Contains("does not contain supported image pages", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageLoader_LoadsGeneratedArchiveAsPageSequence()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("page1.png", 0xFF, 0x00, 0x00),
            ("page2.png", 0x00, 0xFF, 0x00));

        var first = ImageLoader.Load(archivePath);
        var second = ImageLoader.Load(archivePath, pageIndex: 1);

        Assert.Equal(6, first.PixelWidth);
        Assert.Equal(4, first.PixelHeight);
        Assert.NotNull(first.Pages);
        Assert.Equal(0, first.Pages.PageIndex);
        Assert.Equal(2, first.Pages.PageCount);
        Assert.Equal(1, first.Pages.PageSpan);

        Assert.Equal(6, second.PixelWidth);
        Assert.Equal(4, second.PixelHeight);
        Assert.NotNull(second.Pages);
        Assert.Equal(1, second.Pages.PageIndex);
        Assert.Equal(2, second.Pages.PageCount);
        Assert.Contains("archive page 2 of 2", second.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageLoader_LoadsArchiveSpreadAsCompositePageSequence()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("page1.png", 0xFF, 0x00, 0x00),
            ("page2.png", 0x00, 0xFF, 0x00),
            ("page3.png", 0x00, 0x00, 0xFF));

        var spread = ImageLoader.Load(archivePath, archiveSpreadMode: true);

        Assert.Equal(12, spread.PixelWidth);
        Assert.Equal(4, spread.PixelHeight);
        Assert.NotNull(spread.Pages);
        Assert.Equal(0, spread.Pages.PageIndex);
        Assert.Equal(3, spread.Pages.PageCount);
        Assert.Equal(2, spread.Pages.PageSpan);
        Assert.Equal("Pages", spread.Pages.Label);
        Assert.Contains("archive spread, pages 1-2 of 3", spread.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageLoader_LoadsExplicitArchiveCoverWithDecoderProvenance()
    {
        using var temp = TestDirectory.Create();
        var archivePath = Path.Combine(temp.Path, "book.cbz");
        WriteArchive(
            archivePath,
            ("pages/page1.png", 0xFF, 0x00, 0x00),
            ("cover.png", 0x00, 0xFF, 0x00));

        var loaded = ImageLoader.Load(archivePath);

        Assert.NotNull(loaded.Pages);
        Assert.Equal(0, loaded.Pages.PageIndex);
        Assert.Contains("archive cover, page 1 of 2", loaded.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteArchive(string path, params (string Name, byte Red, byte Green, byte Blue)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, red, green, blue) in entries)
            WritePngEntry(archive, name, red, green, blue);
    }

    private static void WriteSevenZipArchive(string path, params (string Name, byte Red, byte Green, byte Blue)[] entries)
    {
        using var output = File.Create(path);
        using var writer = WriterFactory.OpenWriter(
            output,
            ArchiveType.SevenZip,
            new SevenZipWriterOptions(CompressionType.LZMA));
        foreach (var (name, red, green, blue) in entries)
            WritePngEntry(writer, name, red, green, blue);
    }

    private static void WritePngEntry(ZipArchive archive, string name, byte red, byte green, byte blue)
    {
        using var encoded = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(red, green, blue)));
        encoder.Save(encoded);
        encoded.Position = 0;

        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        encoded.CopyTo(stream);
    }

    private static void WriteBytesEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WritePngEntry(IWriter writer, string name, byte red, byte green, byte blue)
    {
        using var encoded = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(red, green, blue)));
        encoder.Save(encoded);
        encoded.Position = 0;

        writer.Write(name, encoded, null);
    }

    private static void WriteBytesEntry(IWriter writer, string name, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        writer.Write(name, stream, null);
    }

    private static BitmapSource CreateBitmap(byte red, byte green, byte blue)
    {
        const int width = 6;
        const int height = 4;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = blue;
            pixels[i + 1] = green;
            pixels[i + 2] = red;
            pixels[i + 3] = 0xFF;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}
