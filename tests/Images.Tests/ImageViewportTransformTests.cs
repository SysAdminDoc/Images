using System.Windows;
using System.Windows.Media;
using Images.Controls;

namespace Images.Tests;

public sealed class ImageViewportTransformTests
{
    [Fact]
    public void Calculate_FitModeCentersLetterboxedImage()
    {
        var matrix = ImageViewportTransform.Calculate(
            pixelWidth: 400,
            pixelHeight: 200,
            viewportWidth: 1000,
            viewportHeight: 1000,
            zoomScale: 1,
            panX: 0,
            panY: 0,
            rotationDegrees: 0,
            flipHorizontal: false,
            flipVertical: false);

        AssertPoint(matrix.Transform(new Point(0, 0)), 0, 250);
        AssertPoint(matrix.Transform(new Point(400, 200)), 1000, 750);
    }

    [Fact]
    public void Calculate_ZoomAndPanTransformImagePixelCoordinates()
    {
        var matrix = ImageViewportTransform.Calculate(
            pixelWidth: 100,
            pixelHeight: 100,
            viewportWidth: 200,
            viewportHeight: 200,
            zoomScale: 2,
            panX: 10,
            panY: -20,
            rotationDegrees: 0,
            flipHorizontal: false,
            flipVertical: false);

        AssertPoint(matrix.Transform(new Point(50, 50)), 110, 80);
        AssertPoint(matrix.Transform(new Point(0, 0)), -90, -120);
    }

    [Fact]
    public void Calculate_RotatesAroundViewportCenter()
    {
        var matrix = ImageViewportTransform.Calculate(
            pixelWidth: 100,
            pixelHeight: 100,
            viewportWidth: 100,
            viewportHeight: 100,
            zoomScale: 1,
            panX: 0,
            panY: 0,
            rotationDegrees: 90,
            flipHorizontal: false,
            flipVertical: false);

        AssertPoint(matrix.Transform(new Point(0, 0)), 100, 0);
        AssertPoint(matrix.Transform(new Point(100, 0)), 100, 100);
    }

    [Fact]
    public void Calculate_FlipsAroundViewportCenter()
    {
        var matrix = ImageViewportTransform.Calculate(
            pixelWidth: 100,
            pixelHeight: 100,
            viewportWidth: 100,
            viewportHeight: 100,
            zoomScale: 1,
            panX: 0,
            panY: 0,
            rotationDegrees: 0,
            flipHorizontal: true,
            flipVertical: false);

        AssertPoint(matrix.Transform(new Point(0, 0)), 100, 0);
        AssertPoint(matrix.Transform(new Point(100, 100)), 0, 100);
    }

    [Fact]
    public void Calculate_InvalidInputsReturnSafeIdentityOrFitTransform()
    {
        var invalidSize = ImageViewportTransform.Calculate(0, 100, 100, 100, 1, 0, 0, 0, false, false);
        Assert.Equal(Matrix.Identity, invalidSize);

        var invalidTransformValues = ImageViewportTransform.Calculate(
            pixelWidth: 100,
            pixelHeight: 100,
            viewportWidth: 100,
            viewportHeight: 100,
            zoomScale: double.NaN,
            panX: double.PositiveInfinity,
            panY: double.NegativeInfinity,
            rotationDegrees: double.NaN,
            flipHorizontal: false,
            flipVertical: false);

        AssertPoint(invalidTransformValues.Transform(new Point(100, 100)), 100, 100);
    }

    private static void AssertPoint(Point actual, double expectedX, double expectedY)
    {
        Assert.Equal(expectedX, actual.X, precision: 6);
        Assert.Equal(expectedY, actual.Y, precision: 6);
    }
}
