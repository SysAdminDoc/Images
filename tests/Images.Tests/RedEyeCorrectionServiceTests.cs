using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class RedEyeCorrectionServiceTests
{
    [Fact]
    public void NormalizeMark_ClampsCoordinatesRadiusStrengthAndThreshold()
    {
        var mark = RedEyeCorrectionService.NormalizeMark(
            new PixelCoordinate(120, -8),
            radius: 900,
            strength: -1,
            threshold: 4,
            imageWidth: 50,
            imageHeight: 30);

        Assert.Equal(49, mark.X);
        Assert.Equal(0, mark.Y);
        Assert.Equal(RedEyeCorrectionService.MaxRadius, mark.Radius);
        Assert.Equal(RedEyeCorrectionService.MinStrength, mark.Strength);
        Assert.Equal(RedEyeCorrectionService.MaxThreshold, mark.Threshold);
    }

    [Fact]
    public void EditParameters_RoundTripMarks()
    {
        var marks = new[]
        {
            new RedEyeCorrectionMark(4, 5, 18, 0.85, 0.35),
            new RedEyeCorrectionMark(8, 9, 22, 0.65, 0.2)
        };

        var parameters = RedEyeCorrectionService.ToEditParameters(marks);
        var roundTrip = RedEyeCorrectionService.FromParameters(parameters);

        Assert.Equal(2, roundTrip.Count);
        Assert.Equal(18, roundTrip[0].Radius);
        Assert.Contains("2 marks", RedEyeCorrectionService.CreateLabel(roundTrip), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_ReducesRedDominantPixelsInsideMarkedArea()
    {
        using var image = CreateTestImage();
        var originalCenter = ReadRgba(image, 2, 2);
        var originalNonRed = ReadRgba(image, 1, 1);
        var originalOutside = ReadRgba(image, 0, 0);

        RedEyeCorrectionService.Apply(
            image,
            new[] { new RedEyeCorrectionMark(2, 2, 2.5, 1, 0.1) });

        var correctedCenter = ReadRgba(image, 2, 2);
        Assert.True(correctedCenter.Red < originalCenter.Red);
        Assert.Equal(originalCenter.Alpha, correctedCenter.Alpha);
        Assert.Equal(originalNonRed, ReadRgba(image, 1, 1));
        Assert.Equal(originalOutside, ReadRgba(image, 0, 0));
    }

    private static MagickImage CreateTestImage()
    {
        var pixels = new byte[5 * 5 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 32;
            pixels[index + 1] = 32;
            pixels[index + 2] = 32;
            pixels[index + 3] = 255;
        }

        SetPixel(pixels, 5, 2, 2, 235, 24, 24);
        SetPixel(pixels, 5, 1, 1, 70, 80, 78);

        var image = new MagickImage(MagickColors.Black, 5, 5);
        image.ImportPixels(pixels, new PixelImportSettings(5, 5, StorageType.Char, PixelMapping.RGBA));
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
