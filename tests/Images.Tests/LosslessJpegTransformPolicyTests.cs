using Images.Services;

namespace Images.Tests;

public sealed class LosslessJpegTransformPolicyTests
{
    private static readonly JpegMcuSize Mcu8 = new(8, 8);

    [Fact]
    public void PlanCrop_AlignedSelectionNeedsNoConfirmation()
    {
        var plan = LosslessJpegTransformPolicy.PlanCrop(
            new PixelSelection(16, 8, 24, 16),
            imageWidth: 80,
            imageHeight: 64,
            Mcu8);

        Assert.Equal(new PixelSelection(16, 8, 24, 16), plan.AlignedSelection);
        Assert.True(plan.IsExact);
        Assert.False(plan.RequiresTrimConfirmation);
        Assert.Contains("already aligned", plan.UserMessage);
    }

    [Fact]
    public void PlanCrop_TrimsUnalignedInteriorSelectionInward()
    {
        var plan = LosslessJpegTransformPolicy.PlanCrop(
            new PixelSelection(11, 10, 31, 27),
            imageWidth: 80,
            imageHeight: 64,
            Mcu8);

        Assert.Equal(new PixelSelection(16, 16, 24, 16), plan.AlignedSelection);
        Assert.True(plan.RequiresTrimConfirmation);
        Assert.Equal(5, plan.TrimLeft);
        Assert.Equal(6, plan.TrimTop);
        Assert.Equal(2, plan.TrimRight);
        Assert.Equal(5, plan.TrimBottom);
    }

    [Fact]
    public void PlanCrop_PreservesSourceEdgeWhenSelectionTouchesImageEdge()
    {
        var plan = LosslessJpegTransformPolicy.PlanCrop(
            new PixelSelection(13, 9, 27, 21),
            imageWidth: 40,
            imageHeight: 30,
            Mcu8);

        Assert.Equal(new PixelSelection(16, 16, 24, 14), plan.AlignedSelection);
        Assert.True(plan.RequiresTrimConfirmation);
        Assert.Equal(3, plan.TrimLeft);
        Assert.Equal(7, plan.TrimTop);
        Assert.Equal(0, plan.TrimRight);
        Assert.Equal(0, plan.TrimBottom);
    }

    [Fact]
    public void PlanCrop_RejectsSelectionThatCannotContainAlignedBlock()
    {
        var plan = LosslessJpegTransformPolicy.PlanCrop(
            new PixelSelection(3, 3, 4, 4),
            imageWidth: 40,
            imageHeight: 30,
            Mcu8);

        Assert.Null(plan.AlignedSelection);
        Assert.False(plan.CanApplyLosslessly);
        Assert.Contains("smaller than one", plan.UserMessage);
    }

    [Fact]
    public void PlanRotation_AlignedImageNeedsNoConfirmation()
    {
        var plan = LosslessJpegTransformPolicy.PlanRotation(
            imageWidth: 80,
            imageHeight: 64,
            LosslessJpegRotation.Rotate90,
            Mcu8);

        Assert.Equal(new PixelSelection(0, 0, 80, 64), plan.PreservedSourceBounds);
        Assert.True(plan.IsExact);
        Assert.False(plan.RequiresTrimConfirmation);
    }

    [Fact]
    public void PlanRotation_UnalignedImageReportsRightAndBottomTrim()
    {
        var plan = LosslessJpegTransformPolicy.PlanRotation(
            imageWidth: 81,
            imageHeight: 66,
            LosslessJpegRotation.Rotate270,
            Mcu8);

        Assert.Equal(new PixelSelection(0, 0, 80, 64), plan.PreservedSourceBounds);
        Assert.True(plan.RequiresTrimConfirmation);
        Assert.Equal(1, plan.TrimRight);
        Assert.Equal(2, plan.TrimBottom);
        Assert.Contains("before rotating 270 degrees", plan.UserMessage);
    }

    [Fact]
    public void PlanRotation_RejectsInvalidImageDimensions()
    {
        var plan = LosslessJpegTransformPolicy.PlanRotation(
            imageWidth: 0,
            imageHeight: 64,
            LosslessJpegRotation.Rotate180,
            Mcu8);

        Assert.Null(plan.PreservedSourceBounds);
        Assert.False(plan.CanApplyLosslessly);
        Assert.Contains("valid JPEG", plan.UserMessage);
    }
}
