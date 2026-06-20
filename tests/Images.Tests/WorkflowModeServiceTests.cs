using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class WorkflowModeServiceTests
{
    [Fact]
    public void SetMode_PersistsAndRetrieves()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        service.SetMode(WorkflowMode.Review);

        Assert.Equal(WorkflowMode.Review, service.CurrentMode);
    }

    [Fact]
    public void CurrentMode_DefaultsToViewer()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        Assert.Equal(WorkflowMode.Viewer, service.CurrentMode);
    }

    [Theory]
    [InlineData(WorkflowMode.Viewer, false)]
    [InlineData(WorkflowMode.Review, true)]
    [InlineData(WorkflowMode.Organize, false)]
    public void GetPreset_ReturnsCorrectReviewModeFlag(WorkflowMode mode, bool expectedReview)
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        var preset = service.GetPreset(mode);

        Assert.Equal(expectedReview, preset.ReviewModeActive);
    }

    [Theory]
    [InlineData(WorkflowMode.Viewer, "Viewer")]
    [InlineData(WorkflowMode.Review, "Review")]
    [InlineData(WorkflowMode.Edit, "Edit")]
    [InlineData(WorkflowMode.Diagnostics, "Diagnostics")]
    public void DisplayName_ReturnsNonEmpty(WorkflowMode mode, string expected)
    {
        Assert.Equal(expected, WorkflowModeService.DisplayName(mode));
    }
}
