using System.Windows;
using Images.Services;

namespace Images.Tests;

public sealed class ReferenceBoardLayoutServiceTests
{
    [Fact]
    public void NextCascadedPosition_AdvancesAcrossColumnsThenRows()
    {
        Assert.Equal(new Point(240, 180), ReferenceBoardLayoutService.NextCascadedPosition(0));
        Assert.Equal(new Point(282, 180), ReferenceBoardLayoutService.NextCascadedPosition(1));
        Assert.Equal(new Point(240, 214), ReferenceBoardLayoutService.NextCascadedPosition(10));
    }

    [Fact]
    public void ClampPosition_KeepsItemInsideCanvas()
    {
        var clamped = ReferenceBoardLayoutService.ClampPosition(
            left: 500,
            top: -20,
            width: 240,
            height: 180,
            canvasWidth: 600,
            canvasHeight: 400);

        Assert.Equal(new Point(360, 0), clamped);
    }

    [Fact]
    public void CalculateContentBounds_AddsPaddingAndClampsToCanvas()
    {
        var bounds = ReferenceBoardLayoutService.CalculateContentBounds(
        [
            new ReferenceBoardItemBounds(10, 20, 120, 90),
            new ReferenceBoardItemBounds(550, 360, 120, 80)
        ],
        padding: 40,
        canvasWidth: 700,
        canvasHeight: 500);

        Assert.Equal(new Rect(0, 0, 700, 480), bounds);
    }

    [Fact]
    public void CalculateContentBounds_ReturnsEmptyForNoVisibleItems()
    {
        var bounds = ReferenceBoardLayoutService.CalculateContentBounds(
        [
            new ReferenceBoardItemBounds(10, 20, 0, 90)
        ]);

        Assert.True(bounds.IsEmpty);
    }
}
