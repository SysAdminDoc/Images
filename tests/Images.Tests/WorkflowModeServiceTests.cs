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

        service.SetMode(WorkflowMode.Organize);

        Assert.Equal(WorkflowMode.Organize, service.CurrentMode);
    }

    [Fact]
    public void CurrentMode_DefaultsToViewer()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        Assert.Equal(WorkflowMode.Viewer, service.CurrentMode);
    }

    [Fact]
    public void CurrentMode_IgnoresRemovedLegacyMode()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        settings.SetString("viewer.workflow-mode", "Review");
        var service = new WorkflowModeService(settings);

        Assert.Equal(WorkflowMode.Viewer, service.CurrentMode);
    }

    [Theory]
    [InlineData(WorkflowMode.Viewer, true, false, false)]
    [InlineData(WorkflowMode.Organize, false, true, true)]
    [InlineData(WorkflowMode.Edit, false, false, false)]
    public void GetPreset_ReturnsExpectedSurfaceFlags(
        WorkflowMode mode,
        bool expectedFilmstrip,
        bool expectedMetadataHud,
        bool expectedGallery)
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var service = new WorkflowModeService(settings);

        var preset = service.GetPreset(mode);

        Assert.Equal(expectedFilmstrip, preset.FilmstripVisible);
        Assert.Equal(expectedMetadataHud, preset.MetadataHudVisible);
        Assert.Equal(expectedGallery, preset.GalleryOpen);
    }

    [Theory]
    [InlineData(WorkflowMode.Viewer, "Viewer")]
    [InlineData(WorkflowMode.Edit, "Edit")]
    [InlineData(WorkflowMode.Diagnostics, "Diagnostics")]
    public void DisplayName_ReturnsNonEmpty(WorkflowMode mode, string expected)
    {
        Assert.Equal(expected, WorkflowModeService.DisplayName(mode));
    }
}
