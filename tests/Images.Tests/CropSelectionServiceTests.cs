using Images.Services;

namespace Images.Tests;

public sealed class CropSelectionServiceTests
{
    [Fact]
    public void Normalize_ClampsSelectionToImageBounds()
    {
        var selection = CropSelectionService.Normalize(
            new PixelSelection(-3, 2, 20, 12),
            pixelWidth: 10,
            pixelHeight: 8);

        Assert.Equal(new PixelSelection(0, 2, 10, 6), selection);
    }

    [Fact]
    public void Normalize_RejectsEmptyOrDimensionlessImages()
    {
        Assert.Null(CropSelectionService.Normalize(new PixelSelection(0, 0, 0, 4), 10, 8));
        Assert.Null(CropSelectionService.Normalize(new PixelSelection(0, 0, 4, 4), 0, 8));
        Assert.Null(CropSelectionService.Normalize(null, 10, 8));
    }

    [Fact]
    public void ToEditParameters_UsesInvariantPixelCoordinates()
    {
        var parameters = CropSelectionService.ToEditParameters(new PixelSelection(1, 2, 3, 4));

        Assert.Equal("1", parameters["x"]);
        Assert.Equal("2", parameters["y"]);
        Assert.Equal("3", parameters["width"]);
        Assert.Equal("4", parameters["height"]);
    }
}
