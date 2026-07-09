using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly CommandShortcutService _shortcutService;
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
        _shortcutService = new CommandShortcutService(settings);
        RefreshOcrStatusCommand = new RelayCommand(RefreshOcrStatus);
        OpenOcrLanguageSettingsCommand = new RelayCommand(OpenOcrLanguageSettings);
        OpenAppDataCommand = new RelayCommand(OpenAppData);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        RefreshStorageDetailCommand = new RelayCommand(RefreshStorageDetail);
        ClearThumbnailCacheCommand = new RelayCommand(ClearThumbnailCache);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        ClearRecoveryLogCommand = new RelayCommand(ClearRecoveryLog);
        ClearNetworkLogCommand = new RelayCommand(ClearNetworkLog);
        ApplyShortcutCommand = new RelayCommand(ApplyShortcut, p => p is ShortcutSettingRow);
        ResetShortcutCommand = new RelayCommand(ResetShortcut, p => p is ShortcutSettingRow);
        RefreshShortcutRows();
        RefreshStorageDetail();
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

    public IReadOnlyList<ThemeOption> AvailableThemes { get; } =
    [
        new ThemeOption(Strings.SettingsThemeDark, "dark"),
        new ThemeOption(Strings.SettingsThemeLight, "latte"),
        new ThemeOption(Strings.SettingsThemeSystem, "system"),
    ];

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

    public ThemeOption SelectedTheme
    {
        get
        {
            var persisted = _settings.GetString(Keys.ThemeMode, "dark") ?? "dark";
            return AvailableThemes.FirstOrDefault(option =>
                       string.Equals(option.Key, persisted, StringComparison.OrdinalIgnoreCase))
                   ?? AvailableThemes[0];
        }
        set
        {
            if (value is null) return;

            _settings.SetString(Keys.ThemeMode, value.Key);
            ThemeService.ApplyFromSettings(_settings);
            Raise(nameof(SelectedTheme));
            SetStatus(
                Strings.Format(nameof(Strings.SettingsThemeChangedStatusFormat), value.DisplayName),
                SettingsStatusToneKind.Success);
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

    public bool SiblingFolderAutoSwitch
    {
        get => _settings.GetBool(Keys.SiblingFolderAutoSwitch, false);
        set
        {
            _settings.SetBool(Keys.SiblingFolderAutoSwitch, value);
            Raise(nameof(SiblingFolderAutoSwitch));
            SetStatus(
                value ? Strings.SettingsSiblingFolderOnStatus : Strings.SettingsSiblingFolderOffStatus,
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

    public ICommand ApplyShortcutCommand { get; }

    public ICommand ResetShortcutCommand { get; }

    public ObservableCollection<ShortcutSettingRow> ShortcutRows { get; } = new();

    public string HotkeySummary
    {
        get
        {
            var snapshots = _shortcutService.GetSnapshots();
            var customizedCount = snapshots.Count(s => s.IsCustomized);
            var format = customizedCount == 0
                ? Strings.SettingsHotkeySummaryDefaultFormat
                : Strings.SettingsHotkeySummaryCustomizedFormat;
            return string.Format(CultureInfo.CurrentCulture, format, snapshots.Count, customizedCount);
        }
    }

    public string DiagnosticsStorageSummary =>
        Strings.SettingsDiagnosticsStorageSummary;

    private string _storageDetail = "";

    public string StorageDetail
    {
        get => _storageDetail;
        private set
        {
            if (_storageDetail == value) return;
            _storageDetail = value;
            Raise(nameof(StorageDetail));
        }
    }

    public ICommand RefreshStorageDetailCommand { get; }

    public ICommand ClearThumbnailCacheCommand { get; }

    public ICommand ClearLogsCommand { get; }

    public ICommand ClearRecoveryLogCommand { get; }

    public ICommand ClearNetworkLogCommand { get; }

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

    private void RefreshShortcutRows()
    {
        ShortcutRows.Clear();
        foreach (var item in _shortcutService.GetSnapshots())
        {
            ShortcutRows.Add(new ShortcutSettingRow(
                item.Id,
                item.Name,
                item.Category,
                item.Shortcut,
                item.DefaultShortcut,
                item.IsCustomized));
        }

        Raise(nameof(HotkeySummary));
    }

    private void ApplyShortcut(object? parameter)
    {
        if (parameter is not ShortcutSettingRow row)
            return;

        var result = _shortcutService.SetShortcut(row.Id, row.ShortcutText);
        switch (result.Kind)
        {
            case ShortcutUpdateResultKind.Saved:
            case ShortcutUpdateResultKind.Reset:
                RefreshShortcutRows();
                var refreshed = _shortcutService.GetDefinition(row.Id);
                SetStatus(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        result.Kind == ShortcutUpdateResultKind.Saved
                            ? Strings.SettingsHotkeySavedFormat
                            : Strings.SettingsHotkeyResetFormat,
                        refreshed.Name,
                        _shortcutService.GetShortcutText(row.Id)),
                    SettingsStatusToneKind.Success);
                break;
            case ShortcutUpdateResultKind.Conflict:
                SetStatus(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.SettingsHotkeyConflictFormat,
                        result.ConflictDefinition?.Name ?? row.ActionName,
                        row.ShortcutText),
                    SettingsStatusToneKind.Warning);
                break;
            case ShortcutUpdateResultKind.Invalid:
            case ShortcutUpdateResultKind.UnknownCommand:
                SetStatus(
                    string.Format(CultureInfo.CurrentCulture, Strings.SettingsHotkeyInvalidFormat, row.ActionName),
                    SettingsStatusToneKind.Warning);
                break;
        }
    }

    private void ResetShortcut(object? parameter)
    {
        if (parameter is not ShortcutSettingRow row)
            return;

        _shortcutService.ResetShortcut(row.Id);
        RefreshShortcutRows();
        SetStatus(
            string.Format(
                CultureInfo.CurrentCulture,
                Strings.SettingsHotkeyResetFormat,
                row.ActionName,
                _shortcutService.GetShortcutText(row.Id)),
            SettingsStatusToneKind.Success);
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

    private void RefreshStorageDetail()
    {
        try
        {
            var lines = new List<string>();
            AddStoreSize(lines, "Thumbnail cache", "thumbs");
            AddStoreSize(lines, "Logs", "Logs");
            AddStoreSize(lines, "Recovery log", "recovery");
            AddStoreSize(lines, "Semantic index", "semantic");
            AddStoreSize(lines, "Models", "models");
            AddStoreSize(lines, "Diagnostics", "diagnostics");
            AddStoreSize(lines, "Wallpaper", "wallpaper");

            var settingsDb = Path.Combine(AppStorage.TryGetAppDirectory() ?? "", "settings.db");
            if (File.Exists(settingsDb))
                lines.Add($"Settings DB: {FormatBytes(new FileInfo(settingsDb).Length)}");

            var networkLog = Path.Combine(AppStorage.TryGetAppDirectory() ?? "", "network-egress.jsonl");
            if (File.Exists(networkLog))
                lines.Add($"Network log: {FormatBytes(new FileInfo(networkLog).Length)}");

            StorageDetail = lines.Count > 0
                ? string.Join("\n", lines)
                : "No local data found.";
        }
        catch
        {
            StorageDetail = "Could not read storage sizes.";
        }
    }

    private static void AddStoreSize(List<string> lines, string label, string subdir)
    {
        var dir = AppStorage.TryGetAppDirectory(subdir);
        if (dir is null || !Directory.Exists(dir)) return;

        try
        {
            var size = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            if (size > 0)
                lines.Add($"{label}: {FormatBytes(size)}");
        }
        catch
        {
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    private void ClearThumbnailCache()
    {
        try
        {
            ThumbnailCache.Instance.Clear();
            SetStatus("Thumbnail cache cleared.", SettingsStatusToneKind.Success);
            RefreshStorageDetail();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not clear thumbnails: {ex.Message}", SettingsStatusToneKind.Warning);
        }
    }

    private void ClearLogs()
    {
        try
        {
            var dir = AppStorage.TryGetAppDirectory("Logs");
            if (dir is not null && Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "images-*.log"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            SetStatus("Log files cleared.", SettingsStatusToneKind.Success);
            RefreshStorageDetail();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not clear logs: {ex.Message}", SettingsStatusToneKind.Warning);
        }
    }

    private void ClearRecoveryLog()
    {
        try
        {
            var file = Path.Combine(
                AppStorage.TryGetAppDirectory("recovery") ?? "", "recovery-log.jsonl");
            if (File.Exists(file)) File.Delete(file);
            SetStatus("Recovery log cleared.", SettingsStatusToneKind.Success);
            RefreshStorageDetail();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not clear recovery log: {ex.Message}", SettingsStatusToneKind.Warning);
        }
    }

    private void ClearNetworkLog()
    {
        try
        {
            NetworkEgressService.ClearAll();
            SetStatus("Network activity log cleared.", SettingsStatusToneKind.Success);
            RefreshStorageDetail();
        }
        catch (Exception ex)
        {
            SetStatus($"Could not clear network log: {ex.Message}", SettingsStatusToneKind.Warning);
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

public sealed record ThemeOption(string DisplayName, string Key)
{
    public override string ToString() => DisplayName;
}

public sealed class ShortcutSettingRow : ObservableObject
{
    private string _shortcutText;
    private bool _isCustomized;

    public ShortcutSettingRow(
        string id,
        string actionName,
        string category,
        string shortcutText,
        string defaultShortcutText,
        bool isCustomized)
    {
        Id = id;
        ActionName = actionName;
        Category = category;
        _shortcutText = shortcutText;
        DefaultShortcutText = defaultShortcutText;
        _isCustomized = isCustomized;
    }

    public string Id { get; }
    public string ActionName { get; }
    public string Category { get; }
    public string DefaultShortcutText { get; }

    public string ShortcutText
    {
        get => _shortcutText;
        set => Set(ref _shortcutText, value);
    }

    public bool IsCustomized
    {
        get => _isCustomized;
        set
        {
            if (Set(ref _isCustomized, value))
                Raise(nameof(StateText));
        }
    }

    public string StateText => IsCustomized
        ? Strings.SettingsHotkeyStateCustom
        : Strings.SettingsHotkeyStateDefault;
}
