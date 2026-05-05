using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class PixelInspectorServiceTests
{
    [Fact]
    public void TryMapViewportPointToPixel_InvertsViewportMatrix()
    {
        var matrix = new Matrix(2, 0, 0, 2, 10, 20);

        var mapped = PixelInspectorService.TryMapViewportPointToPixel(
            matrix,
            new Point(14.4, 24.2),
            pixelWidth: 10,
            pixelHeight: 10,
            out var coordinate);

        Assert.True(mapped);
        Assert.Equal(new PixelCoordinate(2, 2), coordinate);
    }

    [Fact]
    public void TryMapViewportPointToPixel_RejectsPointsOutsideImage()
    {
        var mapped = PixelInspectorService.TryMapViewportPointToPixel(
            Matrix.Identity,
            new Point(10, 2),
            pixelWidth: 10,
            pixelHeight: 10,
            out _);

        Assert.False(mapped);
    }

    [Fact]
    public void TryMapElementPointToPixel_MapsScaledElementCoordinates()
    {
        var mapped = PixelInspectorService.TryMapElementPointToPixel(
            new Point(50, 25),
            elementWidth: 100,
            elementHeight: 50,
            pixelWidth: 20,
            pixelHeight: 10,
            out var coordinate);

        Assert.True(mapped);
        Assert.Equal(new PixelCoordinate(10, 5), coordinate);
    }

    [Fact]
    public void CalculateSelection_NormalizesDragDirectionAndIncludesEndpoints()
    {
        var selection = PixelInspectorService.CalculateSelection(
            new PixelCoordinate(8, 2),
            new PixelCoordinate(3, 6));

        Assert.Equal(new PixelSelection(3, 2, 6, 5), selection);
        Assert.Equal("6 x 5 px at 3, 2", selection.DisplayText);
    }

    [Fact]
    public void SamplePixel_ReturnsHexRgbHsvAndAlpha()
    {
        var source = CreateBgraBitmap(
            width: 2,
            height: 1,
            [
                0, 0, 255, 255,
                30, 20, 10, 128
            ]);

        var red = PixelInspectorService.SamplePixel(source, new PixelCoordinate(0, 0));
        var second = PixelInspectorService.SamplePixel(source, new PixelCoordinate(1, 0));

        Assert.Equal("#FF0000", red.Hex);
        Assert.Equal("RGB 255, 0, 0", red.Rgb);
        Assert.Equal("HSV 0, 100%, 100%", red.Hsv);
        Assert.Equal("A 255", red.Alpha);

        Assert.Equal("#0A141E", second.Hex);
        Assert.Equal("RGB 10, 20, 30", second.Rgb);
        Assert.Equal("A 128", second.Alpha);
    }

    private static BitmapSource CreateBgraBitmap(int width, int height, byte[] pixels)
    {
        var source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        source.Freeze();
        return source;
    }
}
