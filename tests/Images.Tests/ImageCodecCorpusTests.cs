using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageCodecCorpusTests
{
    [Theory]
    [InlineData("sample.png")]
    [InlineData("sample.jpg")]
    public void Load_GeneratedWicRasterCorpus_DecodesStaticImage(string fileName)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, fileName);
        WriteWicBitmap(path);

        var result = ImageLoader.Load(path);

        Assert.Equal(6, result.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        Assert.Null(result.Animation);
        Assert.Null(result.Pages);
        Assert.False(string.IsNullOrWhiteSpace(result.DecoderUsed));
    }

    [Fact]
    public void Load_GeneratedWebPCorpus_DecodesThroughAvailableCodec()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sample.webp");
        WriteMagickBitmap(path, MagickFormat.WebP);

        var result = ImageLoader.Load(path);

        Assert.Equal(6, result.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        Assert.Null(result.Animation);
        Assert.Null(result.Pages);
        Assert.False(string.IsNullOrWhiteSpace(result.DecoderUsed));
    }

    [Fact]
    public void Load_GeneratedMultiPageTiffCorpus_ReportsPageSequence()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "pages.tiff");
        WriteMultiPageTiff(path);

        var first = ImageLoader.Load(path);
        var second = ImageLoader.Load(path, pageIndex: 1);

        Assert.Equal(6, first.PixelWidth);
        Assert.Equal(4, first.PixelHeight);
        Assert.NotNull(first.Pages);
        Assert.Equal(0, first.Pages.PageIndex);
        Assert.Equal(2, first.Pages.PageCount);
        Assert.Equal("Page", first.Pages.Label);

        Assert.Equal(6, second.PixelWidth);
        Assert.Equal(4, second.PixelHeight);
        Assert.NotNull(second.Pages);
        Assert.Equal(1, second.Pages.PageIndex);
        Assert.Equal(2, second.Pages.PageCount);
        Assert.Contains("2 of 2", second.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("animated.gif", MagickFormat.Gif)]
    public void Load_GeneratedAnimatedCorpus_ReturnsAnimationSequence(string fileName, MagickFormat format)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, fileName);
        WriteAnimated(path, format);

        var result = ImageLoader.Load(path);

        Assert.Equal(6, result.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        Assert.NotNull(result.Animation);
        Assert.Equal(2, result.Animation.Frames.Count);
        Assert.Equal(2, result.Animation.Delays.Count);
        Assert.Null(result.Pages);
        Assert.Contains("animated", result.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_GeneratedSvgVectorCorpus_DecodesWithoutExternalAsset()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile(
            "vector.svg",
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="6" height="4" viewBox="0 0 6 4">
              <rect width="6" height="4" fill="#1e1e2e"/>
              <circle cx="3" cy="2" r="1.5" fill="#89b4fa"/>
            </svg>
            """);

        var result = ImageLoader.Load(path);

        Assert.Equal(6, result.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        Assert.Null(result.Animation);
        Assert.Null(result.Pages);
        Assert.Contains("Magick.NET", result.DecoderUsed, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".webp")]
    [InlineData(".tif")]
    [InlineData(".gif")]
    [InlineData(".apng")]
    public void Save_GeneratedExportCorpus_RoundTripsThroughLoader(string extension)
    {
        using var temp = TestDirectory.Create();
        var target = Path.Combine(temp.Path, "export" + extension);
        var source = CreateBitmap();

        var savedPath = ImageExportService.Save(source, target);
        var result = ImageLoader.Load(savedPath);

        Assert.Equal(Path.GetFullPath(target), savedPath);
        Assert.Equal(6, result.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        Assert.False(string.IsNullOrWhiteSpace(result.DecoderUsed));
    }

    private static void WriteWicBitmap(string path)
    {
        BitmapEncoder encoder = Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            ? new JpegBitmapEncoder { QualityLevel = 92 }
            : new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap()));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static void WriteMultiPageTiff(string path)
    {
        var encoder = new TiffBitmapEncoder
        {
            Compression = TiffCompressOption.Lzw
        };
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap()));
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(0xFF, 0xB4, 0x89, 0xFA)));

        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static void WriteMagickBitmap(string path, MagickFormat format)
    {
        using var image = CreateMagickFrame(MagickColors.Green);
        image.Format = format;
        image.Write(path);
    }

    private static void WriteAnimated(string path, MagickFormat format)
    {
        using var collection = new MagickImageCollection();
        var first = CreateMagickFrame(MagickColors.Red);
        var second = CreateMagickFrame(MagickColors.Blue);

        first.AnimationDelay = 8;
        second.AnimationDelay = 8;
        first.AnimationIterations = 0;
        second.AnimationIterations = 0;
        first.Format = format;
        second.Format = format;

        collection.Add(first);
        collection.Add(second);
        collection.Write(path);
    }

    private static MagickImage CreateMagickFrame(IMagickColor<ushort> color)
        => new(color, 6, 4);

    private static BitmapSource CreateBitmap(
        byte alpha = 0xFF,
        byte red = 0xC9,
        byte green = 0xCB,
        byte blue = 0xFF)
    {
        const int width = 6;
        const int height = 4;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(blue - (i / 4 % width));
            pixels[i + 1] = (byte)(green - (i / stride));
            pixels[i + 2] = red;
            pixels[i + 3] = alpha;
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
