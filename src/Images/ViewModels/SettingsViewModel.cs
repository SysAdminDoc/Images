using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Images.Localization;
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

    private readonly SettingsService _settings;
    private OcrCapabilityService.OcrCapabilityStatus _ocrStatus = OcrCapabilityService.GetStatus();
    private string? _settingsStatusText;
    private SettingsStatusToneKind _settingsStatusTone = SettingsStatusToneKind.Info;

    public SettingsViewModel()
        : this(SettingsService.Instance)
    {
    }

    internal SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        RefreshOcrStatusCommand = new RelayCommand(RefreshOcrStatus);
        OpenOcrLanguageSettingsCommand = new RelayCommand(OpenOcrLanguageSettings);
        OpenAppDataCommand = new RelayCommand(OpenAppData);
        OpenLogsCommand = new RelayCommand(OpenLogs);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ---- Language ----

    /// <summary>
    /// Available locale options for the language picker. Each item is a display-name / IETF-tag
    /// pair. Empty string = system default (whatever <see cref="CultureInfo.CurrentUICulture"/>
    /// resolves to at startup).
    /// </summary>
    public IReadOnlyList<LocaleOption> AvailableLocales { get; } = new[]
    {
        new LocaleOption(Strings.SettingsLanguageSystemDefault, string.Empty),
        new LocaleOption("English", "en"),
    };

    public LocaleOption SelectedLocale
    {
        get
        {
            var persisted = _settings.GetString(Keys.Locale, string.Empty) ?? string.Empty;
            foreach (var opt in AvailableLocales)
            {
                if (string.Equals(opt.Tag, persisted, StringComparison.OrdinalIgnoreCase))
                    return opt;
            }
            return AvailableLocales[0];
        }
        set
        {
            var tag = value.Tag;
            _settings.SetString(Keys.Locale, tag);

            // Apply to the static Culture immediately so any runtime-generated strings (code-behind,
            // toasts, status messages) pick up the new locale for the remainder of this session.
            Strings.Culture = string.IsNullOrEmpty(tag)
                ? null
                : new CultureInfo(tag);

            Raise(nameof(SelectedLocale));
            SetStatus(Strings.SettingsLanguageRestartNotice, SettingsStatusToneKind.Warning);
        }
    }

    // ---- Viewer ----

    public bool RememberWindowPlacement
    {
        get => _settings.GetBool(Keys.RememberWindowPlacement, true);
        set
        {
            _settings.SetBool(Keys.RememberWindowPlacement, value);
            Raise(nameof(RememberWindowPlacement));
            SetStatus(
                value ? Strings.SettingsRememberWindowOnStatus : Strings.SettingsRememberWindowOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool FilmstripVisibleOnStartup
    {
        get => _settings.GetBool(Keys.FilmstripVisible, true);
        set
        {
            _settings.SetBool(Keys.FilmstripVisible, value);
            Raise(nameof(FilmstripVisibleOnStartup));
            SetStatus(
                value ? Strings.SettingsFilmstripOnStatus : Strings.SettingsFilmstripOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool MetadataHudVisibleOnStartup
    {
        get => _settings.GetBool(Keys.MetadataHudVisible, false);
        set
        {
            _settings.SetBool(Keys.MetadataHudVisible, value);
            Raise(nameof(MetadataHudVisibleOnStartup));
            SetStatus(
                value ? Strings.SettingsMetadataHudOnStatus : Strings.SettingsMetadataHudOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ConfirmRecycleBinDeletes
    {
        get => _settings.GetBool(Keys.ConfirmRecycleBinDelete, true);
        set
        {
            _settings.SetBool(Keys.ConfirmRecycleBinDelete, value);
            Raise(nameof(ConfirmRecycleBinDeletes));
            SetStatus(
                value
                    ? Strings.SettingsRecycleBinConfirmOnStatus
                    : Strings.SettingsRecycleBinConfirmOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ReduceMotion
    {
        get => _settings.GetBool(Keys.AccessibilityReduceMotion, false);
        set
        {
            _settings.SetBool(Keys.AccessibilityReduceMotion, value);
            Raise(nameof(ReduceMotion));
            SetStatus(
                value ? Strings.SettingsReduceMotionOnStatus : Strings.SettingsReduceMotionOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool HighContrastMode
    {
        get => _settings.GetBool(Keys.AccessibilityHighContrast, false);
        set
        {
            _settings.SetBool(Keys.AccessibilityHighContrast, value);
            ThemeService.ApplyFromSettings(_settings);
            Raise(nameof(HighContrastMode));
            SetStatus(
                value ? Strings.SettingsHighContrastOnStatus : Strings.SettingsHighContrastOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ArchiveRightToLeft
    {
        get => _settings.GetBool(Keys.ArchiveRightToLeft, false);
        set
        {
            _settings.SetBool(Keys.ArchiveRightToLeft, value);
            Raise(nameof(ArchiveRightToLeft));
            SetStatus(
                value ? Strings.SettingsArchiveRtlOnStatus : Strings.SettingsArchiveRtlOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ArchiveOldScanFilter
    {
        get => _settings.GetBool(Keys.ArchiveOldScanFilter, false);
        set
        {
            _settings.SetBool(Keys.ArchiveOldScanFilter, value);
            Raise(nameof(ArchiveOldScanFilter));
            SetStatus(
                value ? Strings.SettingsArchiveCleanScanOnStatus : Strings.SettingsArchiveCleanScanOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ArchiveSpreadMode
    {
        get => _settings.GetBool(Keys.ArchiveSpreadMode, false);
        set
        {
            _settings.SetBool(Keys.ArchiveSpreadMode, value);
            Raise(nameof(ArchiveSpreadMode));
            SetStatus(
                value ? Strings.SettingsArchiveSpreadOnStatus : Strings.SettingsArchiveSpreadOffStatus,
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
                    ? Strings.SettingsUpdateCheckOnStatus
                    : Strings.SettingsUpdateCheckOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public string UpdateCheckDescription =>
        Strings.SettingsUpdateCheckDescription;

    // ---- Text extraction ----

    public bool IsOcrAvailable => _ocrStatus.IsAvailable;

    public string OcrStatusTitle => _ocrStatus.StatusTitle;

    public string OcrStatusDescription => _ocrStatus.StatusDetail;

    public string OcrLanguageSummary => _ocrStatus.LanguageSummary;

    public string OcrStatusBadge => _ocrStatus.BadgeText;

    public ICommand RefreshOcrStatusCommand { get; }

    public ICommand OpenOcrLanguageSettingsCommand { get; }

    public ICommand OpenAppDataCommand { get; }

    public ICommand OpenLogsCommand { get; }

    public string HotkeySummary =>
        Strings.SettingsHotkeySummary;

    public string DiagnosticsStorageSummary =>
        Strings.SettingsDiagnosticsStorageSummary;

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
        SetStatus(Strings.SettingsOcrRefreshedStatus, SettingsStatusToneKind.Success);
    }

    private void OpenOcrLanguageSettings()
    {
        try
        {
            ShellIntegration.OpenShellTarget("ms-settings:regionlanguage");
            SetStatus(Strings.SettingsLanguageSettingsOpenedStatus, SettingsStatusToneKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(
                string.Format(CultureInfo.CurrentCulture, Strings.SettingsLanguageSettingsFailedFormat, ex.Message),
                SettingsStatusToneKind.Warning);
        }
    }

    private void OpenAppData()
    {
        OpenStoragePath(AppStorage.TryGetAppDirectory(), Strings.SettingsAppDataOpenedStatus);
    }

    private void OpenLogs()
    {
        OpenStoragePath(AppStorage.TryGetAppDirectory("Logs"), Strings.SettingsLogsOpenedStatus);
    }

    private void OpenStoragePath(string? path, string successMessage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus(Strings.SettingsStorageUnavailableStatus, SettingsStatusToneKind.Warning);
                return;
            }

            Directory.CreateDirectory(path);
            ShellIntegration.OpenShellTarget(path);
            SetStatus(successMessage, SettingsStatusToneKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(
                string.Format(CultureInfo.CurrentCulture, Strings.SettingsStorageOpenFailedFormat, ex.Message),
                SettingsStatusToneKind.Warning);
        }
    }
}

/// <summary>
/// A display-name / IETF-tag pair for the language picker ComboBox.
/// </summary>
public sealed record LocaleOption(string DisplayName, string Tag)
{
    public override string ToString() => DisplayName;
}
