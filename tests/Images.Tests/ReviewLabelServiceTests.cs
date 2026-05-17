using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ReviewLabelServiceTests
{
    [Fact]
    public void SetRatingAndLabel_WriteAndReadXmpSidecar()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();

        var rating = service.SetRating(image, 5);
        var label = service.SetLabel(image, ReviewLabelKind.Pick);
        var state = service.ReadState(image);

        Assert.True(rating.Success);
        Assert.True(label.Success);
        Assert.Equal(5, state.Rating);
        Assert.Equal(ReviewLabelKind.Pick, state.Label);
        Assert.True(File.Exists(image + ".xmp"));
        Assert.Contains("ReviewLabel", File.ReadAllText(image + ".xmp"));
    }

    [Fact]
    public void Restore_RevertsPreviousReviewState()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 4);
        var mutation = service.SetLabel(image, ReviewLabelKind.Reject);

        service.Restore(image, mutation.Previous);
        var restored = service.ReadState(image);

        Assert.Equal(4, restored.Rating);
        Assert.Equal(ReviewLabelKind.None, restored.Label);
    }

    [Fact]
    public void SetRating_NullClearsRatingAndPreservesLabel()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 3);
        service.SetLabel(image, ReviewLabelKind.Pick);

        service.SetRating(image, null);
        var state = service.ReadState(image);

        Assert.Null(state.Rating);
        Assert.Equal(ReviewLabelKind.Pick, state.Label);
    }

    private static string WritePng(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            Enumerable.Repeat((byte)0x80, 16).ToArray(),
            8);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
