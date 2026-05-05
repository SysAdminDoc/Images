namespace Images.Services;

public enum LosslessJpegRotation
{
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270,
}

public readonly record struct JpegMcuSize(int Width, int Height)
{
    public static JpegMcuSize Conservative420 { get; } = new(16, 16);

    public bool IsValid => Width > 0 && Height > 0;

    public override string ToString() => $"{Width}x{Height}";
}

public sealed record LosslessJpegCropPlan(
    PixelSelection RequestedSelection,
    PixelSelection? AlignedSelection,
    JpegMcuSize McuSize,
    int TrimLeft,
    int TrimTop,
    int TrimRight,
    int TrimBottom,
    string UserMessage)
{
    public bool CanApplyLosslessly => AlignedSelection.HasValue;
    public bool RequiresTrimConfirmation => TrimLeft > 0 || TrimTop > 0 || TrimRight > 0 || TrimBottom > 0;
    public bool IsExact => CanApplyLosslessly && !RequiresTrimConfirmation;
}

public sealed record LosslessJpegRotationPlan(
    int ImageWidth,
    int ImageHeight,
    LosslessJpegRotation Rotation,
    JpegMcuSize McuSize,
    PixelSelection? PreservedSourceBounds,
    int TrimRight,
    int TrimBottom,
    string UserMessage)
{
    public bool CanApplyLosslessly => PreservedSourceBounds.HasValue;
    public bool RequiresTrimConfirmation => TrimRight > 0 || TrimBottom > 0;
    public bool IsExact => CanApplyLosslessly && !RequiresTrimConfirmation;
}

public static class LosslessJpegTransformPolicy
{
    public static LosslessJpegCropPlan PlanCrop(
        PixelSelection requestedSelection,
        int imageWidth,
        int imageHeight,
        JpegMcuSize? mcuSize = null)
    {
        var mcu = NormalizeMcuSize(mcuSize);
        var normalized = CropSelectionService.Normalize(requestedSelection, imageWidth, imageHeight);
        if (normalized is not { } requested || !mcu.IsValid)
        {
            return new LosslessJpegCropPlan(
                requestedSelection,
                AlignedSelection: null,
                mcu,
                TrimLeft: 0,
                TrimTop: 0,
                TrimRight: 0,
                TrimBottom: 0,
                UserMessage: "Select a valid JPEG crop area before using lossless writeback.");
        }

        var requestedRight = requested.X + requested.Width;
        var requestedBottom = requested.Y + requested.Height;

        var left = AlignStartInward(requested.X, mcu.Width);
        var top = AlignStartInward(requested.Y, mcu.Height);
        var right = AlignEndInward(requestedRight, imageWidth, mcu.Width);
        var bottom = AlignEndInward(requestedBottom, imageHeight, mcu.Height);

        var trimLeft = left - requested.X;
        var trimTop = top - requested.Y;
        var trimRight = requestedRight - right;
        var trimBottom = requestedBottom - bottom;

        if (right <= left || bottom <= top)
        {
            return new LosslessJpegCropPlan(
                requested,
                AlignedSelection: null,
                mcu,
                trimLeft,
                trimTop,
                Math.Max(0, trimRight),
                Math.Max(0, trimBottom),
                $"The selected area is smaller than one {mcu} JPEG MCU block after alignment. Use normal export or choose a larger crop.");
        }

        var aligned = new PixelSelection(left, top, right - left, bottom - top);
        var userMessage = trimLeft == 0 && trimTop == 0 && trimRight == 0 && trimBottom == 0
            ? $"This crop is already aligned to the JPEG MCU grid ({mcu})."
            : $"JPEG MCU alignment will trim {FormatTrim(trimLeft, trimTop, trimRight, trimBottom)} before lossless writeback.";

        return new LosslessJpegCropPlan(
            requested,
            aligned,
            mcu,
            trimLeft,
            trimTop,
            trimRight,
            trimBottom,
            userMessage);
    }

    public static LosslessJpegRotationPlan PlanRotation(
        int imageWidth,
        int imageHeight,
        LosslessJpegRotation rotation,
        JpegMcuSize? mcuSize = null)
    {
        var mcu = NormalizeMcuSize(mcuSize);
        if (imageWidth <= 0 || imageHeight <= 0 || !mcu.IsValid)
        {
            return new LosslessJpegRotationPlan(
                imageWidth,
                imageHeight,
                rotation,
                mcu,
                PreservedSourceBounds: null,
                TrimRight: 0,
                TrimBottom: 0,
                UserMessage: "Open a valid JPEG image before using lossless rotation.");
        }

        var trimRight = imageWidth % mcu.Width;
        var trimBottom = imageHeight % mcu.Height;
        var preservedWidth = imageWidth - trimRight;
        var preservedHeight = imageHeight - trimBottom;

        if (preservedWidth <= 0 || preservedHeight <= 0)
        {
            return new LosslessJpegRotationPlan(
                imageWidth,
                imageHeight,
                rotation,
                mcu,
                PreservedSourceBounds: null,
                trimRight,
                trimBottom,
                $"The image is smaller than one {mcu} JPEG MCU block after alignment. Use normal export instead.");
        }

        var preserved = new PixelSelection(0, 0, preservedWidth, preservedHeight);
        var userMessage = trimRight == 0 && trimBottom == 0
            ? $"This image is already aligned to the JPEG MCU grid ({mcu}) for lossless rotation."
            : $"JPEG MCU alignment will trim {FormatTrim(0, 0, trimRight, trimBottom)} before rotating {RotationLabel(rotation)}.";

        return new LosslessJpegRotationPlan(
            imageWidth,
            imageHeight,
            rotation,
            mcu,
            preserved,
            trimRight,
            trimBottom,
            userMessage);
    }

    private static JpegMcuSize NormalizeMcuSize(JpegMcuSize? mcuSize)
    {
        var value = mcuSize ?? JpegMcuSize.Conservative420;
        return value.IsValid ? value : new JpegMcuSize(0, 0);
    }

    private static int AlignStartInward(int value, int mcu)
    {
        if (value <= 0) return 0;
        return RoundUp(value, mcu);
    }

    private static int AlignEndInward(int value, int imageSize, int mcu)
    {
        if (value >= imageSize) return imageSize;
        return RoundDown(value, mcu);
    }

    private static int RoundDown(int value, int multiple)
    {
        if (multiple <= 0) return value;
        return value - (value % multiple);
    }

    private static int RoundUp(int value, int multiple)
    {
        if (multiple <= 0) return value;
        var remainder = value % multiple;
        return remainder == 0 ? value : value + multiple - remainder;
    }

    private static string FormatTrim(int left, int top, int right, int bottom)
    {
        var parts = new List<string>(4);
        if (left > 0) parts.Add($"{left} px from the left");
        if (top > 0) parts.Add($"{top} px from the top");
        if (right > 0) parts.Add($"{right} px from the right");
        if (bottom > 0) parts.Add($"{bottom} px from the bottom");
        return parts.Count == 0 ? "0 px" : string.Join(", ", parts);
    }

    private static string RotationLabel(LosslessJpegRotation rotation) => rotation switch
    {
        LosslessJpegRotation.Rotate90 => "90 degrees",
        LosslessJpegRotation.Rotate180 => "180 degrees",
        LosslessJpegRotation.Rotate270 => "270 degrees",
        _ => $"{(int)rotation} degrees",
    };
}
