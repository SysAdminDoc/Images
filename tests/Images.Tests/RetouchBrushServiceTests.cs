using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class RetouchBrushServiceTests
{
    [Fact]
    public void NormalizeStroke_ClampsSourceTargetRadiusAndStrength()
    {
        var stroke = RetouchBrushService.NormalizeStroke(
            new PixelCoordinate(-5, 99),
            new PixelCoordinate(100, -4),
            radius: 900,
            strength: -1,
            heal: true,
            imageWidth: 20,
            imageHeight: 10);

        Assert.Equal(0, stroke.SourceX);
        Assert.Equal(9, stroke.SourceY);
        Assert.Equal(19, stroke.TargetX);
        Assert.Equal(0, stroke.TargetY);
        Assert.Equal(RetouchBrushService.MaxRadius, stroke.Radius);
        Assert.Equal(RetouchBrushService.MinStrength, stroke.Strength);
        Assert.True(stroke.Heal);
    }

    [Fact]
    public void EditParameters_RoundTripCloneAndHealStrokes()
    {
        var strokes = new[]
        {
            new RetouchBrushStroke(1, 2, 7, 8, 24, 0.8, Heal: false),
            new RetouchBrushStroke(3, 4, 9, 10, 18, 0.7, Heal: true)
        };

        var parameters = RetouchBrushService.ToEditParameters(strokes);
        var roundTrip = RetouchBrushService.FromParameters(parameters);

        Assert.Equal(2, roundTrip.Count);
        Assert.False(roundTrip[0].Heal);
        Assert.True(roundTrip[1].Heal);
        Assert.Contains("clone", RetouchBrushService.CreateLabel(roundTrip), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("heal", RetouchBrushService.CreateLabel(roundTrip), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_CloneStampBlendsSourceIntoTarget()
    {
        using var image = CreateTestImage();
        var originalTarget = ReadRgba(image, 6, 6);
        var source = ReadRgba(image, 2, 2);

        RetouchBrushService.Apply(
            image,
            new[] { new RetouchBrushStroke(2, 2, 6, 6, 4, 1, Heal: false) });

        var correctedTarget = ReadRgba(image, 6, 6);
        Assert.NotEqual(originalTarget, correctedTarget);
        Assert.True(Math.Abs(correctedTarget.Red - source.Red) < Math.Abs(originalTarget.Red - source.Red));
        Assert.Equal(originalTarget.Alpha, correctedTarget.Alpha);
    }

    private static MagickImage CreateTestImage()
    {
        var pixels = new byte[9 * 9 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 20;
            pixels[index + 1] = 40;
            pixels[index + 2] = 180;
            pixels[index + 3] = 255;
        }

        SetPixel(pixels, 9, 2, 2, 220, 80, 40);

        var image = new MagickImage(MagickColors.Black, 9, 9);
        image.ImportPixels(pixels, new PixelImportSettings(9, 9, StorageType.Char, PixelMapping.RGBA));
        return image;
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, byte red, byte green, byte blue)
    {
        var index = ((y * width) + x) * 4;
        pixels[index] = red;
        pixels[index + 1] = green;
        pixels[index + 2] = blue;
        pixels[index + 3] = 255;
    }

    private static Rgba ReadRgba(MagickImage image, int x, int y)
    {
        var pixels = image.GetPixels().ToByteArray(PixelMapping.RGBA);
        Assert.NotNull(pixels);
        var index = ((y * (int)image.Width) + x) * 4;
        return new Rgba(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
    }

    private readonly record struct Rgba(byte Red, byte Green, byte Blue, byte Alpha);
}
