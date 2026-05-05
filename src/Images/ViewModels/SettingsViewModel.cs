using System.ComponentModel;
using System.Windows.Input;
using Images.Services;

namespace Images.ViewModels;

/// <summary>
/// Backing ViewModel for the Settings window.  Each property reads and
/// writes SettingsService directly so changes take effect immediately
/// without requiring an explicit OK/Apply step.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    public enum SettingsStatusToneKind { Info, Success, Warning }

    private OcrCapabilityService.OcrCapabilityStatus _ocrStatus = OcrCapabilityService.GetStatus();
    private string? _settingsStatusText;
    private SettingsStatusToneKind _settingsStatusTone = SettingsStatusToneKind.Info;

    public SettingsViewModel()
    {
        RefreshOcrStatusCommand = new RelayCommand(RefreshOcrStatus);
        OpenOcrLanguageSettingsCommand = new RelayCommand(OpenOcrLanguageSettings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ---- Viewer ----

    public bool FilmstripVisibleOnStartup
    {
        get => SettingsService.Instance.GetBool(Keys.FilmstripVisible, true);
        set
        {
            SettingsService.Instance.SetBool(Keys.FilmstripVisible, value);
            Raise(nameof(FilmstripVisibleOnStartup));
            SetStatus(
                value ? "Filmstrip will be shown at startup." : "Filmstrip will stay hidden at startup.",
                SettingsStatusToneKind.Success);
        }
    }

    public bool MetadataHudVisibleOnStartup
    {
        get => SettingsService.Instance.GetBool(Keys.MetadataHudVisible, false);
        set
        {
            SettingsService.Instance.SetBool(Keys.MetadataHudVisible, value);
            Raise(nameof(MetadataHudVisibleOnStartup));
            SetStatus(
                value ? "Metadata overlay will be shown at startup." : "Metadata overlay will stay hidden at startup.",
                SettingsStatusToneKind.Success);
        }
    }

    // ---- Privacy ----

    public bool UpdateCheckEnabled
    {
        get => UpdateCheckService.OptedIn;
        set
        {
            UpdateCheckService.OptedIn = value;
            Raise(nameof(UpdateCheckEnabled));
            SetStatus(
                value
                    ? "Automatic update checks enabled. Images only contacts GitHub Releases."
                    : "Automatic update checks disabled.",
                SettingsStatusToneKind.Success);
        }
    }

    public string UpdateCheckDescription =>
        "Off by default. When enabled, Images pings github.com/SysAdminDoc/Images at startup. No usage data is sent.";

    // ---- Text extraction ----

    public bool IsOcrAvailable => _ocrStatus.IsAvailable;

    public string OcrStatusTitle => _ocrStatus.StatusTitle;

    public string OcrStatusDescription => _ocrStatus.StatusDetail;

    public string OcrLanguageSummary => _ocrStatus.LanguageSummary;

    public string OcrStatusBadge => _ocrStatus.BadgeText;

    public ICommand RefreshOcrStatusCommand { get; }

    public ICommand OpenOcrLanguageSettingsCommand { get; }

    public string? SettingsStatusText
    {
        get => _settingsStatusText;
        private set
        {
            if (_settingsStatusText == value) return;
            _settingsStatusText = value;
            Raise(nameof(SettingsStatusText));
        }
    }

    public SettingsStatusToneKind SettingsStatusTone
    {
        get => _settingsStatusTone;
        private set
        {
            if (_settingsStatusTone == value) return;
            _settingsStatusTone = value;
            Raise(nameof(SettingsStatusTone));
            Raise(nameof(SettingsStatusIcon));
        }
    }

    public string SettingsStatusIcon => SettingsStatusTone switch
    {
        SettingsStatusToneKind.Success => "\uE73E",
        SettingsStatusToneKind.Warning => "\uE783",
        _ => "\uE930"
    };

    private void SetStatus(string message, SettingsStatusToneKind tone)
    {
        SettingsStatusTone = tone;
        SettingsStatusText = message;
    }

    private void RefreshOcrStatus()
    {
        _ocrStatus = OcrCapabilityService.GetStatus();
        Raise(nameof(IsOcrAvailable));
        Raise(nameof(OcrStatusTitle));
        Raise(nameof(OcrStatusDescription));
        Raise(nameof(OcrLanguageSummary));
        Raise(nameof(OcrStatusBadge));
        SetStatus("Text extraction status refreshed.", SettingsStatusToneKind.Success);
    }

    private void OpenOcrLanguageSettings()
    {
        try
        {
            ShellIntegration.OpenShellTarget("ms-settings:regionlanguage");
            SetStatus("Windows language settings opened.", SettingsStatusToneKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open Windows language settings: {ex.Message}", SettingsStatusToneKind.Warning);
        }
    }
}
