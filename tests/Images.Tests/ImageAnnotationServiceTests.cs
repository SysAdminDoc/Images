using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageAnnotationServiceTests
{
    [Fact]
    public void ToEditParameters_RoundTripsThroughParser()
    {
        var items = new[]
        {
            new ImageAnnotationItem(
                ImageAnnotationKind.Rectangle,
                4,
                5,
                20,
                14,
                0,
                0,
                "",
                1,
                "#F38BA8",
                3,
                32,
                []),
            new ImageAnnotationItem(
                ImageAnnotationKind.Freehand,
                0,
                0,
                0,
                0,
                0,
                0,
                "",
                1,
                "#89B4FA",
                4,
                32,
                [new ImageAnnotationPoint(1, 1), new ImageAnnotationPoint(8, 4)])
        };

        var parsed = ImageAnnotationService.FromParameters(ImageAnnotationService.ToEditParameters(items));

        Assert.Equal(2, parsed.Items.Count);
        Assert.Equal(ImageAnnotationKind.Rectangle, parsed.Items[0].Kind);
        Assert.Equal(ImageAnnotationKind.Freehand, parsed.Items[1].Kind);
        Assert.Equal(2, parsed.Items[1].Points.Count);
    }

    [Fact]
    public void Normalize_DropsEmptyItemsAndClampsStyle()
    {
        var normalized = ImageAnnotationService.Normalize(new ImageAnnotationPlan(
        [
            new ImageAnnotationItem(ImageAnnotationKind.Text, 1, 1, 0, 0, 0, 0, "", 1, "", 0, 0, []),
            new ImageAnnotationItem(ImageAnnotationKind.Arrow, 1, 1, 0, 0, 40, 40, "", 0, "", 500, 500, [])
        ]));

        var item = Assert.Single(normalized.Items);
        Assert.Equal(ImageAnnotationKind.Arrow, item.Kind);
        Assert.Equal(1, item.Number);
        Assert.Equal(80, item.StrokeWidth);
        Assert.Equal(220, item.FontSize);
        Assert.Equal("#F38BA8", item.Color);
    }

    [Fact]
    public void Apply_RendersMarkupAndRedactionItems()
    {
        using var image = new MagickImage(MagickColors.White, 96, 72);
        var before = image.ToByteArray(MagickFormat.Png);
        var plan = new ImageAnnotationPlan(
        [
            new ImageAnnotationItem(ImageAnnotationKind.Rectangle, 5, 5, 30, 20, 0, 0, "", 1, "#F38BA8", 4, 28, []),
            new ImageAnnotationItem(ImageAnnotationKind.Ellipse, 45, 5, 28, 20, 0, 0, "", 1, "#89B4FA", 4, 28, []),
            new ImageAnnotationItem(ImageAnnotationKind.Arrow, 8, 48, 0, 0, 58, 52, "", 1, "#A6E3A1", 5, 28, []),
            new ImageAnnotationItem(ImageAnnotationKind.Text, 8, 24, 0, 0, 0, 0, "OK", 1, "#111111", 4, 18, []),
            new ImageAnnotationItem(ImageAnnotationKind.Number, 78, 54, 0, 0, 0, 0, "", 3, "#F9E2AF", 3, 22, []),
            new ImageAnnotationItem(ImageAnnotationKind.Freehand, 0, 0, 0, 0, 0, 0, "", 1, "#89B4FA", 3, 28, [new ImageAnnotationPoint(4, 68), new ImageAnnotationPoint(20, 62), new ImageAnnotationPoint(44, 70)]),
            new ImageAnnotationItem(ImageAnnotationKind.Pixelate, 62, 26, 22, 16, 0, 0, "", 1, "#F38BA8", 3, 28, [])
        ]);

        ImageAnnotationService.Apply(image, plan);

        Assert.Equal((uint)96, image.Width);
        Assert.Equal((uint)72, image.Height);
        Assert.False(before.SequenceEqual(image.ToByteArray(MagickFormat.Png)));
    }
}
