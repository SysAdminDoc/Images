using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;
using ImageMagick.Drawing;

namespace Images.Services;

public enum ImageAnnotationKind
{
    Arrow,
    Text,
    Rectangle,
    Ellipse,
    Number,
    Freehand,
    Blur,
    Pixelate
}

public sealed record ImageAnnotationPoint(double X, double Y);

public sealed record ImageAnnotationItem(
    ImageAnnotationKind Kind,
    double X,
    double Y,
    double Width,
    double Height,
    double EndX,
    double EndY,
    string Text,
    int Number,
    string Color,
    double StrokeWidth,
    double FontSize,
    IReadOnlyList<ImageAnnotationPoint> Points);

public sealed record ImageAnnotationPlan(IReadOnlyList<ImageAnnotationItem> Items)
{
    public static ImageAnnotationPlan Empty { get; } = new([]);

    public bool IsEmpty => Items.Count == 0;

    public string Label => IsEmpty
        ? "Annotations"
        : $"Annotations ({Items.Count})";

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = JsonSerializer.Serialize(Items, ImageAnnotationService.JsonOptions)
        };
}

public static class ImageAnnotationService
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static ImageAnnotationPlan FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("items", out var json) || string.IsNullOrWhiteSpace(json))
            return ImageAnnotationPlan.Empty;

        try
        {
            var items = JsonSerializer.Deserialize<List<ImageAnnotationItem>>(json, JsonOptions) ?? [];
            return Normalize(new ImageAnnotationPlan(items));
        }
        catch (JsonException)
        {
            return ImageAnnotationPlan.Empty;
        }
    }

    public static ImageAnnotationPlan Normalize(ImageAnnotationPlan plan)
    {
        var items = plan.Items
            .Where(item => IsRenderable(item))
            .Select(NormalizeItem)
            .ToList();
        return new ImageAnnotationPlan(items);
    }

    public static void Apply(MagickImage image, ImageAnnotationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = Normalize(plan);
        if (normalized.IsEmpty)
            return;

        foreach (var item in normalized.Items)
        {
            switch (item.Kind)
            {
                case ImageAnnotationKind.Rectangle:
                    DrawRectangle(image, item);
                    break;
                case ImageAnnotationKind.Ellipse:
                    DrawEllipse(image, item);
                    break;
                case ImageAnnotationKind.Arrow:
                    DrawArrow(image, item);
                    break;
                case ImageAnnotationKind.Text:
                    DrawText(image, item);
                    break;
                case ImageAnnotationKind.Number:
                    DrawNumber(image, item);
                    break;
                case ImageAnnotationKind.Freehand:
                    DrawFreehand(image, item);
                    break;
                case ImageAnnotationKind.Blur:
                    ApplyBlur(image, item);
                    break;
                case ImageAnnotationKind.Pixelate:
                    ApplyPixelate(image, item);
                    break;
            }
        }
    }

    public static IReadOnlyDictionary<string, string> ToEditParameters(IEnumerable<ImageAnnotationItem> items)
        => Normalize(new ImageAnnotationPlan(items.ToList())).ToEditParameters();

    private static ImageAnnotationItem NormalizeItem(ImageAnnotationItem item)
    {
        var color = string.IsNullOrWhiteSpace(item.Color) ? "#F38BA8" : item.Color.Trim();
        var stroke = Math.Clamp(double.IsFinite(item.StrokeWidth) ? item.StrokeWidth : 4, 1, 80);
        var font = Math.Clamp(double.IsFinite(item.FontSize) ? item.FontSize : 34, 8, 220);
        var text = item.Text ?? "";
        var points = (item.Points ?? [])
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToList();

        return item with
        {
            X = Finite(item.X),
            Y = Finite(item.Y),
            Width = Math.Max(0, Finite(item.Width)),
            Height = Math.Max(0, Finite(item.Height)),
            EndX = Finite(item.EndX),
            EndY = Finite(item.EndY),
            Text = text,
            Number = Math.Max(1, item.Number),
            Color = color,
            StrokeWidth = stroke,
            FontSize = font,
            Points = points
        };
    }

    private static bool IsRenderable(ImageAnnotationItem item)
        => item.Kind switch
        {
            ImageAnnotationKind.Text => !string.IsNullOrWhiteSpace(item.Text),
            ImageAnnotationKind.Freehand => (item.Points?.Count ?? 0) >= 2,
            ImageAnnotationKind.Arrow => Distance(item.X, item.Y, item.EndX, item.EndY) >= 2,
            ImageAnnotationKind.Number => true,
            _ => item.Width >= 1 && item.Height >= 1
        };

    private static void DrawRectangle(MagickImage image, ImageAnnotationItem item)
    {
        var (x1, y1, x2, y2) = Rect(item);
        new Drawables()
            .FillColor(MagickColors.Transparent)
            .StrokeColor(ParseColor(item.Color))
            .StrokeWidth(item.StrokeWidth)
            .Rectangle(x1, y1, x2, y2)
            .Draw(image);
    }

    private static void DrawEllipse(MagickImage image, ImageAnnotationItem item)
    {
        var (x1, y1, x2, y2) = Rect(item);
        var rx = Math.Max(1, (x2 - x1) / 2);
        var ry = Math.Max(1, (y2 - y1) / 2);
        new Drawables()
            .FillColor(MagickColors.Transparent)
            .StrokeColor(ParseColor(item.Color))
            .StrokeWidth(item.StrokeWidth)
            .Ellipse(x1 + rx, y1 + ry, rx, ry, 0, 360)
            .Draw(image);
    }

    private static void DrawArrow(MagickImage image, ImageAnnotationItem item)
    {
        var color = ParseColor(item.Color);
        var draw = new Drawables()
            .FillColor(MagickColors.Transparent)
            .StrokeColor(color)
            .StrokeWidth(item.StrokeWidth);

        var tangentStartX = item.X;
        var tangentStartY = item.Y;
        if (item.Points.Count >= 4)
        {
            draw.Bezier(item.Points.Take(4).Select(point => new PointD(point.X, point.Y)));
            tangentStartX = item.Points[2].X;
            tangentStartY = item.Points[2].Y;
        }
        else
        {
            draw.Line(item.X, item.Y, item.EndX, item.EndY);
        }

        var angle = Math.Atan2(item.EndY - tangentStartY, item.EndX - tangentStartX);
        var headLength = Math.Max(12, item.StrokeWidth * 4);
        var left = ArrowPoint(item.EndX, item.EndY, angle + Math.PI * 0.82, headLength);
        var right = ArrowPoint(item.EndX, item.EndY, angle - Math.PI * 0.82, headLength);
        draw.Line(item.EndX, item.EndY, left.X, left.Y)
            .Line(item.EndX, item.EndY, right.X, right.Y)
            .Draw(image);
    }

    private static void DrawText(MagickImage image, ImageAnnotationItem item)
    {
        new Drawables()
            .Font("Segoe UI")
            .FontPointSize(item.FontSize)
            .FillColor(ParseColor(item.Color))
            .Text(item.X, item.Y + item.FontSize, item.Text)
            .Draw(image);
    }

    private static void DrawNumber(MagickImage image, ImageAnnotationItem item)
    {
        var radius = Math.Max(12, item.FontSize * 0.55);
        var color = ParseColor(item.Color);
        var label = item.Number.ToString(CultureInfo.InvariantCulture);

        new Drawables()
            .FillColor(color)
            .StrokeColor(color)
            .StrokeWidth(Math.Max(1, item.StrokeWidth))
            .Circle(item.X, item.Y, item.X + radius, item.Y)
            .Draw(image);

        new Drawables()
            .Font("Segoe UI")
            .FontPointSize(radius)
            .FillColor(MagickColors.White)
            .Text(item.X - radius * 0.35, item.Y + radius * 0.35, label)
            .Draw(image);
    }

    private static void DrawFreehand(MagickImage image, ImageAnnotationItem item)
    {
        new Drawables()
            .FillColor(MagickColors.Transparent)
            .StrokeColor(ParseColor(item.Color))
            .StrokeWidth(item.StrokeWidth)
            .Polyline(item.Points.Select(point => new PointD(point.X, point.Y)))
            .Draw(image);
    }

    private static void ApplyBlur(MagickImage image, ImageAnnotationItem item)
    {
        if (!TryCropRegion(image, item, out var geometry))
            return;

        using var region = image.Clone();
        region.Crop(geometry);
        region.Blur(0, Math.Max(4, Math.Min(28, Math.Min(geometry.Width, geometry.Height) * 0.2)));
        image.Composite(region, geometry.X, geometry.Y, CompositeOperator.Over);
    }

    private static void ApplyPixelate(MagickImage image, ImageAnnotationItem item)
    {
        if (!TryCropRegion(image, item, out var geometry))
            return;

        using var region = image.Clone();
        region.Crop(geometry);
        var smallWidth = Math.Max(1, (int)geometry.Width / 10);
        var smallHeight = Math.Max(1, (int)geometry.Height / 10);
        region.Resize(new MagickGeometry((uint)smallWidth, (uint)smallHeight)
        {
            IgnoreAspectRatio = true
        }, FilterType.Point);
        region.Resize(new MagickGeometry(geometry.Width, geometry.Height)
        {
            IgnoreAspectRatio = true
        }, FilterType.Point);
        image.Composite(region, geometry.X, geometry.Y, CompositeOperator.Over);
    }

    private static bool TryCropRegion(MagickImage image, ImageAnnotationItem item, out MagickGeometry geometry)
    {
        var (x1, y1, x2, y2) = Rect(item);
        var left = Math.Clamp((int)Math.Floor(x1), 0, Math.Max(0, (int)image.Width - 1));
        var top = Math.Clamp((int)Math.Floor(y1), 0, Math.Max(0, (int)image.Height - 1));
        var right = Math.Clamp((int)Math.Ceiling(x2), left + 1, (int)image.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(y2), top + 1, (int)image.Height);
        geometry = new MagickGeometry(left, top, (uint)(right - left), (uint)(bottom - top));
        return geometry.Width > 0 && geometry.Height > 0;
    }

    private static (double X1, double Y1, double X2, double Y2) Rect(ImageAnnotationItem item)
    {
        var x1 = Math.Min(item.X, item.X + item.Width);
        var y1 = Math.Min(item.Y, item.Y + item.Height);
        var x2 = Math.Max(item.X, item.X + item.Width);
        var y2 = Math.Max(item.Y, item.Y + item.Height);
        return (x1, y1, x2, y2);
    }

    private static ImageAnnotationPoint ArrowPoint(double x, double y, double angle, double length)
        => new(x + Math.Cos(angle) * length, y + Math.Sin(angle) * length);

    private static IMagickColor<ushort> ParseColor(string color)
    {
        try
        {
            return new MagickColor(color);
        }
        catch
        {
            return MagickColors.Red;
        }
    }

    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));

    private static double Finite(double value)
        => double.IsFinite(value) ? value : 0;
}
