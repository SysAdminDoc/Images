using System.Globalization;
using ImageMagick;

namespace Images.Services;

public readonly record struct PerspectivePoint(double X, double Y);

public sealed record PerspectiveCorrectionPlan(
    PerspectivePoint TopLeft,
    PerspectivePoint TopRight,
    PerspectivePoint BottomRight,
    PerspectivePoint BottomLeft,
    int OutputWidth,
    int OutputHeight)
{
    public string Label => $"Perspective {OutputWidth}x{OutputHeight}";

    public string Summary =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{OutputWidth}x{OutputHeight}, TL {TopLeft.X:0.#},{TopLeft.Y:0.#}, TR {TopRight.X:0.#},{TopRight.Y:0.#}, BR {BottomRight.X:0.#},{BottomRight.Y:0.#}, BL {BottomLeft.X:0.#},{BottomLeft.Y:0.#}");

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["topLeftX"] = Format(TopLeft.X),
            ["topLeftY"] = Format(TopLeft.Y),
            ["topRightX"] = Format(TopRight.X),
            ["topRightY"] = Format(TopRight.Y),
            ["bottomRightX"] = Format(BottomRight.X),
            ["bottomRightY"] = Format(BottomRight.Y),
            ["bottomLeftX"] = Format(BottomLeft.X),
            ["bottomLeftY"] = Format(BottomLeft.Y),
            ["outputWidth"] = OutputWidth.ToString(CultureInfo.InvariantCulture),
            ["outputHeight"] = OutputHeight.ToString(CultureInfo.InvariantCulture)
        };

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public static class PerspectiveCorrectionService
{
    public static PerspectiveCorrectionPlan Identity(int imageWidth, int imageHeight)
        => new(
            new PerspectivePoint(0, 0),
            new PerspectivePoint(Math.Max(0, imageWidth - 1), 0),
            new PerspectivePoint(Math.Max(0, imageWidth - 1), Math.Max(0, imageHeight - 1)),
            new PerspectivePoint(0, Math.Max(0, imageHeight - 1)),
            Math.Max(1, imageWidth),
            Math.Max(1, imageHeight));

    public static PerspectiveCorrectionPlan Normalize(PerspectiveCorrectionPlan plan, int imageWidth, int imageHeight)
    {
        var normalizedWidth = Math.Max(1, imageWidth);
        var normalizedHeight = Math.Max(1, imageHeight);
        var topLeft = Clamp(plan.TopLeft, normalizedWidth, normalizedHeight);
        var topRight = Clamp(plan.TopRight, normalizedWidth, normalizedHeight);
        var bottomRight = Clamp(plan.BottomRight, normalizedWidth, normalizedHeight);
        var bottomLeft = Clamp(plan.BottomLeft, normalizedWidth, normalizedHeight);
        var output = NormalizeOutputSize(plan, topLeft, topRight, bottomRight, bottomLeft, normalizedWidth, normalizedHeight);

        return new PerspectiveCorrectionPlan(
            topLeft,
            topRight,
            bottomRight,
            bottomLeft,
            output.Width,
            output.Height);
    }

    public static PerspectiveCorrectionPlan FromParameters(
        IReadOnlyDictionary<string, string> parameters,
        int imageWidth,
        int imageHeight)
    {
        var identity = Identity(imageWidth, imageHeight);
        return Normalize(
            new PerspectiveCorrectionPlan(
                new PerspectivePoint(
                    ParseDouble(parameters, "topLeftX", identity.TopLeft.X),
                    ParseDouble(parameters, "topLeftY", identity.TopLeft.Y)),
                new PerspectivePoint(
                    ParseDouble(parameters, "topRightX", identity.TopRight.X),
                    ParseDouble(parameters, "topRightY", identity.TopRight.Y)),
                new PerspectivePoint(
                    ParseDouble(parameters, "bottomRightX", identity.BottomRight.X),
                    ParseDouble(parameters, "bottomRightY", identity.BottomRight.Y)),
                new PerspectivePoint(
                    ParseDouble(parameters, "bottomLeftX", identity.BottomLeft.X),
                    ParseDouble(parameters, "bottomLeftY", identity.BottomLeft.Y)),
                ParseInt(parameters, "outputWidth", identity.OutputWidth),
                ParseInt(parameters, "outputHeight", identity.OutputHeight)),
            imageWidth,
            imageHeight);
    }

    public static bool IsIdentity(PerspectiveCorrectionPlan plan, int imageWidth, int imageHeight)
    {
        var normalized = Normalize(plan, imageWidth, imageHeight);
        var identity = Identity(imageWidth, imageHeight);
        return Near(normalized.TopLeft, identity.TopLeft) &&
               Near(normalized.TopRight, identity.TopRight) &&
               Near(normalized.BottomRight, identity.BottomRight) &&
               Near(normalized.BottomLeft, identity.BottomLeft) &&
               normalized.OutputWidth == identity.OutputWidth &&
               normalized.OutputHeight == identity.OutputHeight;
    }

    public static void Apply(MagickImage image, PerspectiveCorrectionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(image);
        var normalized = Normalize(plan, (int)image.Width, (int)image.Height);
        if (IsIdentity(normalized, (int)image.Width, (int)image.Height))
            return;

        image.VirtualPixelMethod = VirtualPixelMethod.Transparent;
        image.BackgroundColor = MagickColors.Transparent;

        var settings = new DistortSettings(DistortMethod.Perspective)
        {
            Bestfit = false,
            Viewport = new MagickGeometry(0, 0, (uint)normalized.OutputWidth, (uint)normalized.OutputHeight)
        };

        image.Distort(
            settings,
            [
                normalized.TopLeft.X, normalized.TopLeft.Y, 0, 0,
                normalized.TopRight.X, normalized.TopRight.Y, normalized.OutputWidth - 1, 0,
                normalized.BottomRight.X, normalized.BottomRight.Y, normalized.OutputWidth - 1, normalized.OutputHeight - 1,
                normalized.BottomLeft.X, normalized.BottomLeft.Y, 0, normalized.OutputHeight - 1
            ]);
    }

    public static PerspectiveCorrectionPlan CreateFromCorners(
        PerspectivePoint topLeft,
        PerspectivePoint topRight,
        PerspectivePoint bottomRight,
        PerspectivePoint bottomLeft,
        int imageWidth,
        int imageHeight)
        => Normalize(
            new PerspectiveCorrectionPlan(
                topLeft,
                topRight,
                bottomRight,
                bottomLeft,
                OutputWidth: 0,
                OutputHeight: 0),
            imageWidth,
            imageHeight);

    private static (int Width, int Height) NormalizeOutputSize(
        PerspectiveCorrectionPlan plan,
        PerspectivePoint topLeft,
        PerspectivePoint topRight,
        PerspectivePoint bottomRight,
        PerspectivePoint bottomLeft,
        int imageWidth,
        int imageHeight)
    {
        var width = plan.OutputWidth > 0
            ? plan.OutputWidth
            : (int)Math.Round(Math.Max(Distance(topLeft, topRight), Distance(bottomLeft, bottomRight)));
        var height = plan.OutputHeight > 0
            ? plan.OutputHeight
            : (int)Math.Round(Math.Max(Distance(topLeft, bottomLeft), Distance(topRight, bottomRight)));

        return (
            Math.Clamp(width, 1, Math.Max(1, imageWidth * 4)),
            Math.Clamp(height, 1, Math.Max(1, imageHeight * 4)));
    }

    private static PerspectivePoint Clamp(PerspectivePoint point, int imageWidth, int imageHeight)
        => new(
            Math.Clamp(Finite(point.X), 0, Math.Max(0, imageWidth - 1)),
            Math.Clamp(Finite(point.Y), 0, Math.Max(0, imageHeight - 1)));

    private static double Distance(PerspectivePoint first, PerspectivePoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> parameters, string key, double fallback)
        => parameters.TryGetValue(key, out var value) &&
           double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int ParseInt(IReadOnlyDictionary<string, string> parameters, string key, int fallback)
        => parameters.TryGetValue(key, out var value) &&
           int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static double Finite(double value)
        => double.IsFinite(value) ? value : 0;

    private static bool Near(PerspectivePoint left, PerspectivePoint right)
        => Math.Abs(left.X - right.X) < 0.01 && Math.Abs(left.Y - right.Y) < 0.01;
}
