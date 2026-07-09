namespace Images.Services;

public enum WorkflowMode
{
    Viewer
}

public sealed record WorkflowModePreset(
    bool FilmstripVisible,
    bool MetadataHudVisible,
    bool GalleryOpen)
{
    public static WorkflowModePreset ForMode(WorkflowMode mode)
        => new(FilmstripVisible: true, MetadataHudVisible: false, GalleryOpen: false);
}

public sealed class WorkflowModeService
{
    private const string SettingsKey = "viewer.workflow-mode";
    private readonly SettingsService _settings;

    public WorkflowModeService(SettingsService? settings = null)
    {
        _settings = settings ?? SettingsService.Instance;
    }

    public WorkflowMode CurrentMode
    {
        get
        {
            var stored = _settings.GetString(SettingsKey, WorkflowMode.Viewer.ToString());
            if (!string.Equals(stored, WorkflowMode.Viewer.ToString(), StringComparison.OrdinalIgnoreCase))
                _settings.SetString(SettingsKey, WorkflowMode.Viewer.ToString());

            return WorkflowMode.Viewer;
        }
    }

    public void SetMode(WorkflowMode mode)
    {
        _settings.SetString(SettingsKey, WorkflowMode.Viewer.ToString());
    }

    public WorkflowModePreset GetPreset(WorkflowMode mode)
        => WorkflowModePreset.ForMode(mode);

    public static string DisplayName(WorkflowMode mode) => "Viewer";

    public static string Description(WorkflowMode mode) => "Default image viewing surface";
}
