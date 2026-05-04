using System.ComponentModel;
using Images.Services;

namespace Images.ViewModels;

/// <summary>
/// Backing ViewModel for the Settings window.  Each property reads and
/// writes SettingsService directly so changes take effect immediately
/// without requiring an explicit OK/Apply step.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
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
}
