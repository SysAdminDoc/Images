using System.Globalization;

namespace Images.Services;

public enum ResizeDimensionMode
{
    Percent,
    Pixels,
    LongEdge,
    ShortEdge,
}

public sealed record ResizeDimensionModeOption(
    ResizeDimensionMode Mode,
    string Label,
    string Description);

public sealed record ResizeFilterPreset(
    string Id,
    string Label,
    string Description);

public sealed record ResizePlanRequest(
    int SourceWidth,
    int SourceHeight,
    ResizeDimensionMode Mode,
    double Percent,
    int Width,
    int Height,
    int Edge,
    bool AspectLocked,
    ResizeFilterPreset Filter);

public sealed record ResizePlan(
    bool IsValid,
    int SourceWidth,
    int SourceHeight,
    int OutputWidth,
    int OutputHeight,
    ResizeDimensionMode Mode,
    bool AspectLocked,
    ResizeFilterPreset Filter,
    string Summary,
    string Error)
{
    public string Label => IsValid
        ? $"Resize {OutputWidth}x{OutputHeight} ({Filter.Label})"
        : "Resize";

    public IReadOnlyDictionary<string, string> ToEditParameters()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["width"] = OutputWidth.ToString(CultureInfo.InvariantCulture),
            ["height"] = OutputHeight.ToString(CultureInfo.InvariantCulture),
            ["ignoreAspectRatio"] = (!AspectLocked).ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ["filter"] = Filter.Id,
            ["mode"] = Mode.ToString(),
            ["sourceWidth"] = SourceWidth.ToString(CultureInfo.InvariantCulture),
            ["sourceHeight"] = SourceHeight.ToString(CultureInfo.InvariantCulture)
        };
}

public static class ResizePlanService
{
    public const int MaxDimension = 30000;

    public static readonly ResizeFilterPreset Lanczos3Filter = new(
        "lanczos3",
        "Lanczos-3",
        "Crisp detail for high-quality downscaling.");

    public static readonly IReadOnlyList<ResizeFilterPreset> FilterPresets =
    [
        Lanczos3Filter,
        new("mitchell", "Mitchell", "Balanced resize with fewer halos."),
        new("bicubic", "Bicubic", "Smooth general-purpose interpolation.")
    ];

    public static readonly IReadOnlyList<ResizeDimensionModeOption> ModeOptions =
    [
        new(ResizeDimensionMode.Percent, "Percent", "Scale both dimensions by a percentage."),
        new(ResizeDimensionMode.Pixels, "Pixels", "Enter target width and height."),
        new(ResizeDimensionMode.LongEdge, "Long edge", "Set the longest side and preserve the source ratio."),
        new(ResizeDimensionMode.ShortEdge, "Short edge", "Set the shortest side and preserve the source ratio.")
    ];

    public static ResizeFilterPreset FindFilter(string? id)
        => FilterPresets.FirstOrDefault(filter => filter.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? Lanczos3Filter;

    public static ResizePlan CreatePlan(ResizePlanRequest request)
    {
        if (request.SourceWidth <= 0 || request.SourceHeight <= 0)
            return Invalid(request, "Open an image with valid pixel dimensions before resizing.");

        if (request.Filter is null)
            return Invalid(request, "Choose a resize filter.");

        var dimensions = request.Mode switch
        {
            ResizeDimensionMode.Percent => FromPercent(request),
            ResizeDimensionMode.Pixels => FromPixels(request),
            ResizeDimensionMode.LongEdge => FromEdge(request, useLongEdge: true),
            ResizeDimensionMode.ShortEdge => FromEdge(request, useLongEdge: false),
            _ => null
        };

        if (dimensions is not { } target)
            return Invalid(request, "Enter positive resize values.");

        if (target.Width <= 0 || target.Height <= 0)
            return Invalid(request, "Resize output must be at least 1 x 1 pixels.");

        if (target.Width > MaxDimension || target.Height > MaxDimension)
            return Invalid(request, $"Resize output cannot exceed {MaxDimension} pixels on either edge.");

        var scaleX = target.Width / (double)request.SourceWidth;
        var scaleY = target.Height / (double)request.SourceHeight;
        var summary = $"{request.SourceWidth} x {request.SourceHeight} -> {target.Width} x {target.Height} " +
                      $"({FormatPercent(scaleX)} x {FormatPercent(scaleY)}, {request.Filter.Label})";

        return new ResizePlan(
            IsValid: true,
            request.SourceWidth,
            request.SourceHeight,
            target.Width,
            target.Height,
            request.Mode,
            request.AspectLocked,
            request.Filter,
            summary,
            Error: "");
    }

    private static (int Width, int Height)? FromPercent(ResizePlanRequest request)
    {
        if (request.Percent <= 0 || double.IsNaN(request.Percent) || double.IsInfinity(request.Percent))
            return null;

        var scale = request.Percent / 100.0;
        return (
            ClampPositive((int)Math.Round(request.SourceWidth * scale, MidpointRounding.AwayFromZero)),
            ClampPositive((int)Math.Round(request.SourceHeight * scale, MidpointRounding.AwayFromZero)));
    }

    private static (int Width, int Height)? FromPixels(ResizePlanRequest request)
    {
        var width = request.Width;
        var height = request.Height;

        if (width <= 0 && height <= 0)
            return null;

        if (!request.AspectLocked)
            return (width > 0 ? width : request.SourceWidth, height > 0 ? height : request.SourceHeight);

        var aspect = request.SourceWidth / (double)request.SourceHeight;
        if (width > 0 && height > 0)
            return FitInside(width, height, aspect);

        if (width > 0)
            return (width, ClampPositive((int)Math.Round(width / aspect, MidpointRounding.AwayFromZero)));

        return (ClampPositive((int)Math.Round(height * aspect, MidpointRounding.AwayFromZero)), height);
    }

    private static (int Width, int Height)? FromEdge(ResizePlanRequest request, bool useLongEdge)
    {
        if (request.Edge <= 0)
            return null;

        var sourceIsLandscape = request.SourceWidth >= request.SourceHeight;
        var targetWidthUsesEdge = useLongEdge ? sourceIsLandscape : !sourceIsLandscape;
        var aspect = request.SourceWidth / (double)request.SourceHeight;

        if (targetWidthUsesEdge)
            return (request.Edge, ClampPositive((int)Math.Round(request.Edge / aspect, MidpointRounding.AwayFromZero)));

        return (ClampPositive((int)Math.Round(request.Edge * aspect, MidpointRounding.AwayFromZero)), request.Edge);
    }

    private static (int Width, int Height) FitInside(int width, int height, double aspect)
    {
        var candidateHeight = ClampPositive((int)Math.Round(width / aspect, MidpointRounding.AwayFromZero));
        if (candidateHeight <= height)
            return (width, candidateHeight);

        var candidateWidth = ClampPositive((int)Math.Round(height * aspect, MidpointRounding.AwayFromZero));
        return (candidateWidth, height);
    }

    private static ResizePlan Invalid(ResizePlanRequest request, string error)
        => new(
            IsValid: false,
            request.SourceWidth,
            request.SourceHeight,
            OutputWidth: 0,
            OutputHeight: 0,
            request.Mode,
            request.AspectLocked,
            request.Filter ?? Lanczos3Filter,
            Summary: error,
            Error: error);

    private static int ClampPositive(int value) => Math.Max(1, value);

    private static string FormatPercent(double scale)
        => (scale * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
