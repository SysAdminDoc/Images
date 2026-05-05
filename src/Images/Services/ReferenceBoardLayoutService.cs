using System.Windows;

namespace Images.Services;

public readonly record struct ReferenceBoardItemBounds(double Left, double Top, double Width, double Height);

public static class ReferenceBoardLayoutService
{
    public const double CanvasWidth = 6400;
    public const double CanvasHeight = 4200;
    public const double DefaultExportPadding = 72;

    public static Point NextCascadedPosition(
        int itemCount,
        double originX = 240,
        double originY = 180,
        double stepX = 42,
        double stepY = 34,
        int columns = 10)
    {
        if (itemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns), "Column count must be positive.");

        var column = itemCount % columns;
        var row = itemCount / columns;
        return new Point(originX + (column * stepX), originY + (row * stepY));
    }

    public static Point ClampPosition(
        double left,
        double top,
        double width,
        double height,
        double canvasWidth = CanvasWidth,
        double canvasHeight = CanvasHeight)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        var maxLeft = Math.Max(0, canvasWidth - safeWidth);
        var maxTop = Math.Max(0, canvasHeight - safeHeight);

        return new Point(
            Math.Clamp(left, 0, maxLeft),
            Math.Clamp(top, 0, maxTop));
    }

    public static Rect CalculateContentBounds(
        IEnumerable<ReferenceBoardItemBounds> items,
        double padding = DefaultExportPadding,
        double canvasWidth = CanvasWidth,
        double canvasHeight = CanvasHeight)
    {
        if (items is null)
            throw new ArgumentNullException(nameof(items));
        if (padding < 0)
            throw new ArgumentOutOfRangeException(nameof(padding), "Padding cannot be negative.");

        var validItems = items
            .Where(item => item.Width > 0 && item.Height > 0)
            .ToArray();

        if (validItems.Length == 0)
            return Rect.Empty;

        var left = Math.Max(0, validItems.Min(item => item.Left) - padding);
        var top = Math.Max(0, validItems.Min(item => item.Top) - padding);
        var right = Math.Min(canvasWidth, validItems.Max(item => item.Left + item.Width) + padding);
        var bottom = Math.Min(canvasHeight, validItems.Max(item => item.Top + item.Height) + padding);

        return new Rect(
            left,
            top,
            Math.Max(1, right - left),
            Math.Max(1, bottom - top));
    }
}
