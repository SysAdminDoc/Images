using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class WorkflowModeServiceTests
{
    [Fact]
    public void SetMode_PersistsViewerOnly()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        service.SetMode(WorkflowMode.Viewer);

        Assert.Equal(WorkflowMode.Viewer, service.CurrentMode);
        Assert.Equal("Viewer", settings.GetString("viewer.workflow-mode", ""));
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
    [InlineData("Review")]
    [InlineData("Organize")]
    [InlineData("Edit")]
    [InlineData("Book")]
    [InlineData("Diagnostics")]
    [InlineData("unknown")]
    public void CurrentMode_NormalizesRemovedLegacyModesToViewer(string storedMode)
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        settings.SetString("viewer.workflow-mode", storedMode);
        var service = new WorkflowModeService(settings);

        Assert.Equal(WorkflowMode.Viewer, service.CurrentMode);
        Assert.Equal("Viewer", settings.GetString("viewer.workflow-mode", ""));
    }

    [Fact]
    public void GetPreset_ReturnsViewerSurfaceFlags()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        var preset = service.GetPreset(WorkflowMode.Viewer);

        Assert.True(preset.FilmstripVisible);
        Assert.False(preset.MetadataHudVisible);
        Assert.False(preset.GalleryOpen);
    }

    [Fact]
    public void DisplayName_ReturnsViewer()
    {
        Assert.Equal("Viewer", WorkflowModeService.DisplayName(WorkflowMode.Viewer));
    }
}
