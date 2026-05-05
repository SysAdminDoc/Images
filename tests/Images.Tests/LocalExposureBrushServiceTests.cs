using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class LocalExposureBrushServiceTests
{
    [Fact]
    public void NormalizeStroke_ClampsCoordinatesRadiusAndStrength()
    {
        var stroke = LocalExposureBrushService.NormalizeStroke(
            new PixelCoordinate(-12, 90),
            radius: 999,
            strength: -4,
            imageWidth: 40,
            imageHeight: 30);

        Assert.Equal(0, stroke.X);
        Assert.Equal(29, stroke.Y);
        Assert.Equal(LocalExposureBrushService.MaxRadius, stroke.Radius);
        Assert.Equal(LocalExposureBrushService.MinStrength, stroke.Strength);
    }

    [Fact]
    public void EditParameters_RoundTripBrushStrokes()
    {
        var strokes = new[]
        {
            new LocalExposureBrushStroke(4, 5, 24, 0.3),
            new LocalExposureBrushStroke(8, 9, 18, -0.45)
        };

        var parameters = LocalExposureBrushService.ToEditParameters(strokes);
        var roundTrip = LocalExposureBrushService.FromParameters(parameters);

        Assert.Equal(2, roundTrip.Count);
        Assert.Equal("Dodge", roundTrip[0].ModeLabel);
        Assert.Equal("Burn", roundTrip[1].ModeLabel);
        Assert.Contains("dodge", LocalExposureBrushService.CreateLabel(roundTrip), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("burn", LocalExposureBrushService.CreateLabel(roundTrip), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_DodgeAndBurnChangeOnlyPaintedPixels()
    {
        using var dodge = new MagickImage(MagickColors.Gray, 9, 9);
        var originalDodgeCenter = ReadRedChannel(dodge, 4, 4);

        LocalExposureBrushService.Apply(
            dodge,
            new[] { new LocalExposureBrushStroke(4, 4, 3, 0.5) });

        Assert.True(ReadRedChannel(dodge, 4, 4) > originalDodgeCenter);
        Assert.Equal(originalDodgeCenter, ReadRedChannel(dodge, 0, 0));

        using var burn = new MagickImage(MagickColors.Gray, 9, 9);
        var originalBurnCenter = ReadRedChannel(burn, 4, 4);

        LocalExposureBrushService.Apply(
            burn,
            new[] { new LocalExposureBrushStroke(4, 4, 3, -0.5) });

        Assert.True(ReadRedChannel(burn, 4, 4) < originalBurnCenter);
        Assert.Equal(originalBurnCenter, ReadRedChannel(burn, 0, 0));
    }

    private static byte ReadRedChannel(MagickImage image, int x, int y)
    {
        var pixels = image.GetPixels().ToByteArray(PixelMapping.RGBA);
        Assert.NotNull(pixels);
        return pixels[((y * (int)image.Width) + x) * 4];
    }
}
