using System.Text.Json;
using System.Text.Json.Serialization;

namespace Images.Services;

public enum WorkflowMode
{
    Viewer,
    Review,
    Organize,
    Edit,
    Book,
    Diagnostics
}

public sealed record WorkflowModePreset(
    bool FilmstripVisible,
    bool MetadataHudVisible,
    bool GalleryOpen,
    bool ReviewModeActive)
{
    public static WorkflowModePreset ForMode(WorkflowMode mode) => mode switch
    {
        WorkflowMode.Viewer => new(FilmstripVisible: true, MetadataHudVisible: false, GalleryOpen: false, ReviewModeActive: false),
        WorkflowMode.Review => new(FilmstripVisible: true, MetadataHudVisible: true, GalleryOpen: false, ReviewModeActive: true),
        WorkflowMode.Organize => new(FilmstripVisible: false, MetadataHudVisible: true, GalleryOpen: true, ReviewModeActive: false),
        WorkflowMode.Edit => new(FilmstripVisible: false, MetadataHudVisible: false, GalleryOpen: false, ReviewModeActive: false),
        WorkflowMode.Book => new(FilmstripVisible: false, MetadataHudVisible: false, GalleryOpen: false, ReviewModeActive: false),
        WorkflowMode.Diagnostics => new(FilmstripVisible: false, MetadataHudVisible: true, GalleryOpen: false, ReviewModeActive: false),
        _ => new(FilmstripVisible: true, MetadataHudVisible: false, GalleryOpen: false, ReviewModeActive: false)
    };
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
            var stored = _settings.GetString(SettingsKey, "Viewer");
            return Enum.TryParse<WorkflowMode>(stored, ignoreCase: true, out var mode)
                ? mode
                : WorkflowMode.Viewer;
        }
    }

    public void SetMode(WorkflowMode mode)
    {
        _settings.SetString(SettingsKey, mode.ToString());
    }

    public WorkflowModePreset GetPreset(WorkflowMode mode)
        => WorkflowModePreset.ForMode(mode);

    public static string DisplayName(WorkflowMode mode) => mode switch
    {
        WorkflowMode.Viewer => "Viewer",
        WorkflowMode.Review => "Review",
        WorkflowMode.Organize => "Organize",
        WorkflowMode.Edit => "Edit",
        WorkflowMode.Book => "Book",
        WorkflowMode.Diagnostics => "Diagnostics",
        _ => mode.ToString()
    };

    public static string Description(WorkflowMode mode) => mode switch
    {
        WorkflowMode.Viewer => "Minimal viewing with filmstrip, no metadata",
        WorkflowMode.Review => "Culling with filmstrip, metadata HUD, and review labels",
        WorkflowMode.Organize => "Gallery with metadata for tagging and sorting",
        WorkflowMode.Edit => "Image-only for editing and crop workflows",
        WorkflowMode.Book => "Archive book reading mode",
        WorkflowMode.Diagnostics => "Metadata HUD with diagnostics focus",
        _ => ""
    };
}
