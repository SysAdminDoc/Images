using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class CullingScoreServiceTests
{
    [Fact]
    public void RankFiles_PrefersSharpBalancedFrameOverFlatFrame()
    {
        using var temp = TestDirectory.Create();
        var flat = Path.Combine(temp.Path, "flat.png");
        var sharp = Path.Combine(temp.Path, "sharp.png");
        WriteSolidImage(flat, 128);
        WritePatternImage(sharp);

        var result = new CullingScoreService().RankFiles([flat, sharp]);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(sharp, result.Items[0].Path);
        Assert.True(result.Items[0].SharpnessScore > result.Items[1].SharpnessScore);
        Assert.Contains("Sharpness", result.Items[0].ReasonsText, StringComparison.Ordinal);
    }

    [Fact]
    public void RankFiles_IncludesExposureWarningForClippedFrame()
    {
        using var temp = TestDirectory.Create();
        var white = Path.Combine(temp.Path, "white.png");
        WriteSolidImage(white, 255);

        var result = new CullingScoreService().RankFiles([white]);

        var item = Assert.Single(result.Items);
        Assert.True(item.ExposureScore < 40);
        Assert.Contains("Highlight clipping", item.ReasonsText, StringComparison.Ordinal);
    }

    [Fact]
    public void RankFiles_UsesExistingRatingsAndReviewLabelsAsReasons()
    {
        using var temp = TestDirectory.Create();
        var image = Path.Combine(temp.Path, "rated.png");
        WriteSolidImage(image, 128);
        var labels = new ReviewLabelService();
        labels.SetRating(image, 5);
        labels.SetLabel(image, ReviewLabelKind.Pick);

        var result = new CullingScoreService(labels).RankFiles([image]);

        var item = Assert.Single(result.Items);
        Assert.Equal(5, item.Rating);
        Assert.Equal(ReviewLabelKind.Pick, item.Label);
        Assert.Contains("5-star rating boost", item.ReasonsText, StringComparison.Ordinal);
        Assert.Contains("Existing pick boost", item.ReasonsText, StringComparison.Ordinal);
    }

    [Fact]
    public void RankFiles_PenalizesLowerRankedSimilarFrames()
    {
        using var temp = TestDirectory.Create();
        var first = Path.Combine(temp.Path, "similar-a.png");
        var second = Path.Combine(temp.Path, "similar-b.png");
        WritePatternImage(first);
        WritePatternImage(second);

        var result = new CullingScoreService().RankFiles([first, second]);

        Assert.Equal(2, result.Items.Count);
        var penalized = Assert.Single(result.Items, item => item.SimilarityPenalty > 0);
        Assert.Equal(second, penalized.Path);
        Assert.Contains("Similarity penalty", penalized.ReasonsText, StringComparison.Ordinal);
    }

    private static void WriteSolidImage(string path, byte value)
    {
        const int width = 32;
        const int height = 32;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = value;
            pixels[offset + 1] = value;
            pixels[offset + 2] = value;
            pixels[offset + 3] = 0xFF;
        }

        WriteBgraPng(path, width, height, stride, pixels);
    }

    private static void WritePatternImage(string path)
    {
        const int width = 32;
        const int height = 32;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var value = ((x / 4) + (y / 4)) % 2 == 0 ? (byte)72 : (byte)184;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 0xFF;
            }
        }

        WriteBgraPng(path, width, height, stride, pixels);
    }

    private static void WriteBgraPng(string path, int width, int height, int stride, byte[] pixels)
    {
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

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
