using Images.Services;

namespace Images.Tests;

public sealed class LaMaInpaintServiceTests
{
    [Fact]
    public void IsAvailable_ReturnsFalse_WhenNoModelsImported()
    {
        Assert.False(LaMaInpaintService.IsAvailable());
    }

    [Fact]
    public void Inpaint_EmptyMaskRegions_ReturnsError()
    {
        var result = LaMaInpaintService.Inpaint(
            "test.jpg",
            Array.Empty<InpaintMaskRegion>(),
            imageWidth: 100,
            imageHeight: 100);

        Assert.False(result.Success);
        Assert.Contains("No mask regions", result.ErrorMessage!);
    }

    [Fact]
    public void Inpaint_NoModel_ReturnsModelNotFoundError()
    {
        var regions = new[] { new InpaintMaskRegion(50, 50, 10) };

        var result = LaMaInpaintService.Inpaint(
            "test.jpg",
            regions,
            imageWidth: 100,
            imageHeight: 100);

        Assert.False(result.Success);
        Assert.Contains("No approved LaMa model", result.ErrorMessage!);
    }

    [Fact]
    public void InpaintMaskRegion_ComputesBounds()
    {
        var region = new InpaintMaskRegion(100, 200, 25);

        Assert.Equal(75, region.Left);
        Assert.Equal(175, region.Top);
        Assert.Equal(50, region.Diameter);
    }
}
