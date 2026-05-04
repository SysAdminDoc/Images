using System.Windows;
using System.Windows.Media;

namespace Images.Controls;

public static class ImageViewportTransform
{
    public static Matrix Calculate(
        int pixelWidth,
        int pixelHeight,
        double viewportWidth,
        double viewportHeight,
        double zoomScale,
        double panX,
        double panY,
        double rotationDegrees,
        bool flipHorizontal,
        bool flipVertical)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 ||
            viewportWidth <= 0 || viewportHeight <= 0 ||
            double.IsNaN(viewportWidth) || double.IsNaN(viewportHeight) ||
            double.IsInfinity(viewportWidth) || double.IsInfinity(viewportHeight))
            return Matrix.Identity;

        var fit = Math.Min(viewportWidth / pixelWidth, viewportHeight / pixelHeight);
        if (fit <= 0 || double.IsNaN(fit) || double.IsInfinity(fit))
            return Matrix.Identity;

        var contentWidth = pixelWidth * fit;
        var contentHeight = pixelHeight * fit;
        var contentLeft = (viewportWidth - contentWidth) / 2;
        var contentTop = (viewportHeight - contentHeight) / 2;
        var centerX = viewportWidth / 2;
        var centerY = viewportHeight / 2;
        if (zoomScale <= 0 || double.IsNaN(zoomScale) || double.IsInfinity(zoomScale))
            zoomScale = 1;
        if (double.IsNaN(panX) || double.IsInfinity(panX))
            panX = 0;
        if (double.IsNaN(panY) || double.IsInfinity(panY))
            panY = 0;
        if (double.IsNaN(rotationDegrees) || double.IsInfinity(rotationDegrees))
            rotationDegrees = 0;

        var group = new TransformGroup();
        group.Children.Add(new MatrixTransform(new Matrix(fit, 0, 0, fit, contentLeft, contentTop)));
        group.Children.Add(new ScaleTransform(flipHorizontal ? -1 : 1, flipVertical ? -1 : 1, centerX, centerY));
        group.Children.Add(new RotateTransform(rotationDegrees, centerX, centerY));
        group.Children.Add(new ScaleTransform(zoomScale, zoomScale, centerX, centerY));
        group.Children.Add(new TranslateTransform(panX, panY));
        return group.Value;
    }
}
