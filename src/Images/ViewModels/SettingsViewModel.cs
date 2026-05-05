using System.ComponentModel;
using System.Diagnostics;
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
    private OcrCapabilityService.OcrCapabilityStatus _ocrStatus = OcrCapabilityService.GetStatus();
    private string? _settingsStatusText;

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
        set { SettingsService.Instance.SetBool(Keys.FilmstripVisible, value); Raise(nameof(FilmstripVisibleOnStartup)); }
    }

    public bool MetadataHudVisibleOnStartup
    {
        get => SettingsService.Instance.GetBool(Keys.MetadataHudVisible, false);
        set { SettingsService.Instance.SetBool(Keys.MetadataHudVisible, value); Raise(nameof(MetadataHudVisibleOnStartup)); }
    }

    // ---- Privacy ----

    public bool UpdateCheckEnabled
    {
        get => UpdateCheckService.OptedIn;
        set { UpdateCheckService.OptedIn = value; Raise(nameof(UpdateCheckEnabled)); }
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

    private void RefreshOcrStatus()
    {
        _ocrStatus = OcrCapabilityService.GetStatus();
        Raise(nameof(IsOcrAvailable));
        Raise(nameof(OcrStatusTitle));
        Raise(nameof(OcrStatusDescription));
        Raise(nameof(OcrLanguageSummary));
        Raise(nameof(OcrStatusBadge));
        SettingsStatusText = "Text extraction status refreshed.";
    }

    private void OpenOcrLanguageSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:regionlanguage",
                UseShellExecute = true,
            });
            SettingsStatusText = "Windows language settings opened.";
        }
        catch (Exception ex)
        {
            SettingsStatusText = $"Could not open Windows language settings: {ex.Message}";
        }
    }
}
