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
    private readonly SettingsTransferService _settingsTransfer;
    private readonly LocalDataStoreRegistry _localDataStores;
    private readonly Func<string, bool> _confirmPrivacyReset;
    private OcrCapabilityService.OcrCapabilityStatus _ocrStatus = OcrCapabilityService.GetStatus();
    private string? _settingsStatusText;
    private SettingsStatusToneKind _settingsStatusTone = SettingsStatusToneKind.Info;

    public SettingsViewModel()
        : this(SettingsService.Instance)
    {
    }

    internal SettingsViewModel(
        SettingsService settings,
        LocalDataStoreRegistry? localDataStores = null,
        Func<string, bool>? confirmPrivacyReset = null,
        SettingsTransferService? settingsTransfer = null)
    {
        _settings = settings;
        _shortcutService = new CommandShortcutService(settings);
        _settingsTransfer = settingsTransfer ?? new SettingsTransferService(settings);
        _localDataStores = localDataStores ?? new LocalDataStoreRegistry();
        _confirmPrivacyReset = confirmPrivacyReset ?? ConfirmPrivacyReset;
        RefreshOcrStatusCommand = new RelayCommand(RefreshOcrStatus);
        OpenOcrLanguageSettingsCommand = new RelayCommand(OpenOcrLanguageSettings);
        OpenAppDataCommand = new RelayCommand(OpenAppData);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        RefreshStorageDetailCommand = new RelayCommand(RefreshStorageDetail);
        ClearThumbnailCacheCommand = new RelayCommand(ClearThumbnailCache);
        ClearLogsCommand = new RelayCommand(ClearLogs);
        ClearRecoveryLogCommand = new RelayCommand(ClearRecoveryLog);
        ClearNetworkLogCommand = new RelayCommand(ClearNetworkLog);
        ClearDerivedDataCommand = new RelayCommand(ClearDerivedData);
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

    public IReadOnlyList<LoupeMagnificationOption> AvailableLoupeMagnifications { get; } =
    [
        new LoupeMagnificationOption("2×", 2.0),
        new LoupeMagnificationOption("3×", 3.0),
        new LoupeMagnificationOption("4×", 4.0),
        new LoupeMagnificationOption("6×", 6.0),
    ];

    public IReadOnlyList<ToneMapOption> AvailableToneMapOperators { get; } =
    [
        new ToneMapOption(Strings.SettingsToneMapReinhard, ToneMapOperator.Reinhard),
        new ToneMapOption(Strings.SettingsToneMapHable, ToneMapOperator.Hable),
        new ToneMapOption(Strings.SettingsToneMapAces, ToneMapOperator.Aces),
    ];

    public LoupeMagnificationOption SelectedLoupeMagnification
    {
        get
        {
            var persisted = _settings.GetDouble(Keys.LoupeFactor, 2.0);
            return AvailableLoupeMagnifications.FirstOrDefault(option =>
                       Math.Abs(option.Factor - persisted) < 0.01)
                   ?? AvailableLoupeMagnifications[0];
        }
        set
        {
            if (value is null) return;
            _settings.SetDouble(Keys.LoupeFactor, value.Factor);
            Raise(nameof(SelectedLoupeMagnification));
            SetStatus(
                Strings.Format(nameof(Strings.SettingsLoupeMagnificationChangedStatusFormat), value.DisplayName),
                SettingsStatusToneKind.Success);
        }
    }

    public ToneMapOption SelectedToneMapOperator
    {
        get
        {
            var persisted = ToneMapService.ParseOperator(
                _settings.GetString(Keys.HdrToneMapOperator, ToneMapOperator.Reinhard.ToString()));
            return AvailableToneMapOperators.First(option => option.Operator == persisted);
        }
        set
        {
            if (value is null) return;
            _settings.SetString(Keys.HdrToneMapOperator, value.Operator.ToString());
            ImageLoader.HdrToneMapOperator = value.Operator;
            Raise(nameof(SelectedToneMapOperator));
            SetStatus(
                Strings.Format(nameof(Strings.SettingsToneMapChangedStatusFormat), value.DisplayName),
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

    public bool RestoreLastSession
    {
        get => _settings.GetBool(Keys.RestoreLastSession, false);
        set
        {
            _settings.SetBool(Keys.RestoreLastSession, value);
            Raise(nameof(RestoreLastSession));
            SetStatus(
                value ? Strings.SettingsRestoreSessionOnStatus : Strings.SettingsRestoreSessionOffStatus,
                SettingsStatusToneKind.Success);
        }
    }

    public bool ColorManagedDisplay
    {
        get => _settings.GetBool(Keys.ColorManagement, false);
        set
        {
            _settings.SetBool(Keys.ColorManagement, value);
            // Static loader flag takes effect on the next image decode.
            ImageLoader.ColorManagedDisplay = value;
            Raise(nameof(ColorManagedDisplay));
            SetStatus(
                value ? Strings.SettingsColorManagementOnStatus : Strings.SettingsColorManagementOffStatus,
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

    public bool StopAtEnds
    {
        get => _settings.GetBool(Keys.StopAtEnds, false);
        set
        {
            _settings.SetBool(Keys.StopAtEnds, value);
            Raise(nameof(StopAtEnds));
            SetStatus(
                value ? Strings.SettingsStopAtEndsOnStatus : Strings.SettingsStopAtEndsOffStatus,
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

    public ICommand ClearDerivedDataCommand { get; }

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

    internal void ExportPortableSettings(string path)
    {
        try
        {
            var result = _settingsTransfer.Export(path);
            SetStatus(
                Strings.Format(
                    nameof(Strings.SettingsTransferExportSuccessFormat),
                    result.SettingsCount,
                    result.HotkeyCount),
                SettingsStatusToneKind.Success);
        }
        catch
        {
            SetStatus(Strings.SettingsTransferExportFailed, SettingsStatusToneKind.Warning);
        }
    }

    internal SettingsTransferPreview? PreviewPortableSettingsImport(string path)
    {
        try
        {
            return _settingsTransfer.PreviewImport(path);
        }
        catch
        {
            SetStatus(Strings.SettingsTransferImportInvalid, SettingsStatusToneKind.Warning);
            return null;
        }
    }

    internal static string BuildPortableSettingsImportPreview(SettingsTransferPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return Strings.Format(
            nameof(Strings.SettingsTransferPreviewFormat),
            preview.Settings.Count,
            preview.Hotkeys.Count,
            preview.IgnoredCount);
    }

    internal void ApplyPortableSettingsImport(SettingsTransferPreview preview)
    {
        try
        {
            _settingsTransfer.ApplyImport(preview);
        }
        catch
        {
            SetStatus(Strings.SettingsTransferImportFailed, SettingsStatusToneKind.Warning);
            return;
        }

        RefreshAfterPortableSettingsImport();
        SetStatus(
            Strings.Format(
                nameof(Strings.SettingsTransferImportSuccessFormat),
                preview.Settings.Count,
                preview.Hotkeys.Count),
            SettingsStatusToneKind.Success);
    }

    private void RefreshAfterPortableSettingsImport()
    {
        var locale = _settings.GetString(Keys.Locale, string.Empty) ?? string.Empty;
        Strings.Culture = string.IsNullOrEmpty(locale) ? null : CultureInfo.GetCultureInfo(locale);
        ImageLoader.HdrToneMapOperator = ToneMapService.ParseOperator(
            _settings.GetString(Keys.HdrToneMapOperator, ToneMapOperator.Reinhard.ToString()));
        ThemeService.ApplyFromSettings(_settings);

        Raise(nameof(SelectedLocale));
        Raise(nameof(SelectedTheme));
        Raise(nameof(SelectedLoupeMagnification));
        Raise(nameof(SelectedToneMapOperator));
        Raise(nameof(RememberWindowPlacement));
        Raise(nameof(RestoreLastSession));
        Raise(nameof(ColorManagedDisplay));
        Raise(nameof(FilmstripVisibleOnStartup));
        Raise(nameof(MetadataHudVisibleOnStartup));
        Raise(nameof(ConfirmRecycleBinDeletes));
        Raise(nameof(SiblingFolderAutoSwitch));
        Raise(nameof(StopAtEnds));
        Raise(nameof(ReduceMotion));
        Raise(nameof(HighContrastMode));
        Raise(nameof(ArchiveRightToLeft));
        Raise(nameof(ArchiveOldScanFilter));
        Raise(nameof(ArchiveSpreadMode));
        RefreshShortcutRows();
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
            StorageDetail = _localDataStores.BuildReport(includePaths: false).TrimEnd();
        }
        catch
        {
            StorageDetail = "Could not read storage sizes.";
        }
    }

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

    private void ClearDerivedData()
    {
        const string prompt = "Clear rebuildable caches, indexes, diagnostics, logs, recovery records, and temporary media? Settings, imported models, smart collections, keyword sets, wallpaper, drafts, backups, and quarantined originals will be preserved.";
        if (!_confirmPrivacyReset(prompt))
        {
            SetStatus("Local data reset canceled.", SettingsStatusToneKind.Info);
            return;
        }

        var result = _localDataStores.ClearPrivacyResetStores();
        RefreshStorageDetail();
        if (result.Succeeded)
        {
            SetStatus(
                $"Cleared {result.ClearedStores} local data stores ({LocalDataStoreRegistry.FormatBytes(result.ClearedBytes)}). Imported models and user-owned recovery files were preserved.",
                SettingsStatusToneKind.Success);
            return;
        }

        SetStatus(
            $"Cleared {result.ClearedStores} local data stores; {result.FailedStoreIds.Count} locked or unavailable stores could not be cleared.",
            SettingsStatusToneKind.Warning);
    }

    private static bool ConfirmPrivacyReset(string prompt)
        => System.Windows.MessageBox.Show(
               prompt,
               "Clear local derived data",
               System.Windows.MessageBoxButton.YesNo,
               System.Windows.MessageBoxImage.Warning,
               System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;

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

/// <summary>A display label / magnification-factor pair for the loupe magnification picker.</summary>
public sealed record LoupeMagnificationOption(string DisplayName, double Factor)
{
    public override string ToString() => DisplayName;
}

public sealed record ToneMapOption(string DisplayName, ToneMapOperator Operator)
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
