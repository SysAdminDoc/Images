using Images.Services;

namespace Images.Tests;

public sealed class ResizePlanServiceTests
{
    [Fact]
    public void CreatePlan_PercentScalesBothDimensions()
    {
        var plan = ResizePlanService.CreatePlan(Request(mode: ResizeDimensionMode.Percent, percent: 50));

        Assert.True(plan.IsValid);
        Assert.Equal(2000, plan.OutputWidth);
        Assert.Equal(1500, plan.OutputHeight);
        Assert.Contains("50% x 50%", plan.Summary);
    }

    [Fact]
    public void CreatePlan_PixelsWithAspectLockUsesEnteredWidth()
    {
        var plan = ResizePlanService.CreatePlan(Request(mode: ResizeDimensionMode.Pixels, width: 1000));

        Assert.True(plan.IsValid);
        Assert.Equal(1000, plan.OutputWidth);
        Assert.Equal(750, plan.OutputHeight);
    }

    [Fact]
    public void CreatePlan_PixelsWithoutAspectLockKeepsExplicitDimensions()
    {
        var plan = ResizePlanService.CreatePlan(Request(
            mode: ResizeDimensionMode.Pixels,
            width: 1200,
            height: 600,
            aspectLocked: false));

        Assert.True(plan.IsValid);
        Assert.Equal(1200, plan.OutputWidth);
        Assert.Equal(600, plan.OutputHeight);
        Assert.Equal("true", plan.ToEditParameters()["ignoreAspectRatio"]);
    }

    [Fact]
    public void CreatePlan_LongEdgePreservesLandscapeRatio()
    {
        var plan = ResizePlanService.CreatePlan(Request(mode: ResizeDimensionMode.LongEdge, edge: 1600));

        Assert.True(plan.IsValid);
        Assert.Equal(1600, plan.OutputWidth);
        Assert.Equal(1200, plan.OutputHeight);
    }

    [Fact]
    public void CreatePlan_ShortEdgePreservesPortraitRatio()
    {
        var plan = ResizePlanService.CreatePlan(new ResizePlanRequest(
            SourceWidth: 3000,
            SourceHeight: 4000,
            Mode: ResizeDimensionMode.ShortEdge,
            Percent: 100,
            Width: 0,
            Height: 0,
            Edge: 1200,
            AspectLocked: true,
            Filter: ResizePlanService.FindFilter("mitchell")));

        Assert.True(plan.IsValid);
        Assert.Equal(1200, plan.OutputWidth);
        Assert.Equal(1600, plan.OutputHeight);
        Assert.Equal("mitchell", plan.ToEditParameters()["filter"]);
    }

    [Fact]
    public void CreatePlan_RejectsDimensionAboveRenderLimit()
    {
        var plan = ResizePlanService.CreatePlan(Request(mode: ResizeDimensionMode.Percent, percent: 1000));

        Assert.False(plan.IsValid);
        Assert.Contains("cannot exceed", plan.Error);
    }

    [Fact]
    public void ToEditParameters_RecordsOutputAndFilter()
    {
        var plan = ResizePlanService.CreatePlan(Request(mode: ResizeDimensionMode.LongEdge, edge: 2000));
        var parameters = plan.ToEditParameters();

        Assert.Equal("2000", parameters["width"]);
        Assert.Equal("1500", parameters["height"]);
        Assert.Equal("false", parameters["ignoreAspectRatio"]);
        Assert.Equal("lanczos3", parameters["filter"]);
    }

    private static ResizePlanRequest Request(
        ResizeDimensionMode mode,
        double percent = 100,
        int width = 0,
        int height = 0,
        int edge = 0,
        bool aspectLocked = true)
        => new(
            SourceWidth: 4000,
            SourceHeight: 3000,
            mode,
            percent,
            width,
            height,
            edge,
            aspectLocked,
            ResizePlanService.Lanczos3Filter);
}
