using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Images.Services;

public sealed class SettingsTransferService
{
    public const int CurrentFormatVersion = 1;
    public const int MaxTransferBytes = BoundedTextFileReader.MaxWorkflowImportBytes;
    internal const int MaxSettings = 64;
    internal const int MaxHotkeys = 128;

    private const string FormatId = "images-settings";

    private static readonly IReadOnlyDictionary<string, Func<string, bool>> PortableSettings =
        new ReadOnlyDictionary<string, Func<string, bool>>(
            new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
            {
                [Keys.RememberWindowPlacement] = IsBool,
                [Keys.FilmstripVisible] = IsBool,
                [Keys.MetadataHudVisible] = IsBool,
                [Keys.ZoomLock] = IsBool,
                [Keys.TransparencyGrid] = IsBool,
                [Keys.LoupeFactor] = IsLoupeFactor,
                [Keys.RestoreLastSession] = IsBool,
                [Keys.ColorManagement] = IsBool,
                [Keys.HdrToneMapOperator] = value => Enum.TryParse<ToneMapOperator>(value, true, out _),
                [Keys.StopAtEnds] = IsBool,
                [Keys.ConfirmRecycleBinDelete] = IsBool,
                [Keys.AccessibilityReduceMotion] = IsBool,
                [Keys.AccessibilityHighContrast] = IsBool,
                [Keys.ArchiveRightToLeft] = IsBool,
                [Keys.ArchiveOldScanFilter] = IsBool,
                [Keys.ArchiveSpreadMode] = IsBool,
                [Keys.Locale] = value => string.IsNullOrEmpty(value) || value.Equals("en", StringComparison.OrdinalIgnoreCase),
                [Keys.ViewerSortMode] = value => Enum.TryParse<DirectorySortMode>(value, true, out _),
                [Keys.SiblingFolderAutoSwitch] = IsBool,
                [Keys.GalleryTileSize] = IsGalleryTileSize,
                [Keys.WritebackBackupPolicy] = value => IsOneOf(value, "none", "same-folder", "app-local"),
                [Keys.WritebackConfirmFirst] = IsBool,
                [Keys.ThemeMode] = value => IsOneOf(value, "dark", "latte", "system", "high-contrast")
            });

    private readonly SettingsService _settings;
    private readonly CommandShortcutService _shortcuts;

    public SettingsTransferService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _shortcuts = new CommandShortcutService(settings);
    }

    public SettingsTransferExportResult Export(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in PortableSettings.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var value = _settings.GetString(definition.Key);
            if (value is null)
                continue;
            if (!IsBoundedText(definition.Key, 128) || !IsBoundedText(value, 256) || !definition.Value(value))
                throw new InvalidDataException($"Portable setting '{definition.Key}' has an invalid value.");
            settings.Add(definition.Key, value);
        }

        var knownActions = _shortcuts.Definitions
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var portableHotkeys = _settings.GetHotkeys()
            .Where(item => knownActions.Contains(item.Action))
            .Where(item => ShortcutGesture.TryCreate(item.Key, item.Modifiers, out _))
            .OrderBy(item => item.Action, StringComparer.Ordinal)
            .ToList();
        if (!_shortcuts.TryValidateOverrides(portableHotkeys, out var hotkeyError))
            throw new InvalidDataException(hotkeyError ?? "Stored hotkeys are not portable.");

        var hotkeys = portableHotkeys
            .Select(item => new SettingsTransferHotkey(item.Action, item.Key, item.Modifiers))
            .ToList();

        var document = new SettingsTransferDocument(FormatId, CurrentFormatVersion, settings, hotkeys);
        var json = JsonSerializer.Serialize(document, SettingsTransferJsonContext.Default.SettingsTransferDocument);
        if (Encoding.UTF8.GetByteCount(json) > MaxTransferBytes)
            throw new InvalidDataException("Settings transfer exceeds the export size limit.");

        WriteAtomically(path, json);
        return new SettingsTransferExportResult(settings.Count, hotkeys.Count);
    }

    public SettingsTransferPreview PreviewImport(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = BoundedTextFileReader.ReadUtf8(path, MaxTransferBytes, "Settings transfer");

        using (var parsed = JsonDocument.Parse(json, new JsonDocumentOptions
               {
                   AllowTrailingCommas = false,
                   CommentHandling = JsonCommentHandling.Disallow,
                   MaxDepth = 16
               }))
        {
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Settings transfer root must be a JSON object.");
            EnsureNoDuplicateProperties(parsed.RootElement);
        }

        SettingsTransferDocument document;
        try
        {
            document = JsonSerializer.Deserialize(json, SettingsTransferJsonContext.Default.SettingsTransferDocument)
                       ?? throw new InvalidDataException("Settings transfer is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Settings transfer JSON is malformed.", ex);
        }

        if (!string.Equals(document.Format, FormatId, StringComparison.Ordinal))
            throw new InvalidDataException("This file is not an Images settings transfer.");
        if (document.Version != CurrentFormatVersion)
            throw new InvalidDataException($"Settings transfer version {document.Version} is not supported.");
        if (document.Settings is null || document.Hotkeys is null)
            throw new InvalidDataException("Settings transfer must contain settings and hotkeys collections.");
        if (document.Settings.Count > MaxSettings || document.Hotkeys.Count > MaxHotkeys)
            throw new InvalidDataException("Settings transfer contains too many entries.");

        var portable = new Dictionary<string, string>(StringComparer.Ordinal);
        var ignoredSettings = 0;
        foreach (var item in document.Settings)
        {
            if (!IsBoundedText(item.Key, 128) || !IsBoundedText(item.Value, 256))
                throw new InvalidDataException("Settings transfer contains an oversized key or value.");

            if (!PortableSettings.TryGetValue(item.Key, out var validator))
            {
                ignoredSettings++;
                continue;
            }

            if (!validator(item.Value))
                throw new InvalidDataException($"Portable setting '{item.Key}' has an invalid value.");
            portable.Add(item.Key, item.Value);
        }

        var knownActions = _shortcuts.Definitions
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        var seenActions = new HashSet<string>(StringComparer.Ordinal);
        var hotkeys = new List<HotkeyOverride>();
        var ignoredHotkeys = 0;
        foreach (var item in document.Hotkeys)
        {
            if (!IsBoundedText(item.Action, 128) ||
                !IsBoundedText(item.Key, 64) ||
                item.Modifiers is null || item.Modifiers.Length > 64)
            {
                throw new InvalidDataException("Settings transfer contains an oversized hotkey entry.");
            }

            if (!seenActions.Add(item.Action))
                throw new InvalidDataException($"Settings transfer repeats hotkey '{item.Action}'.");
            if (!knownActions.Contains(item.Action))
            {
                ignoredHotkeys++;
                continue;
            }

            hotkeys.Add(new HotkeyOverride(item.Action, item.Key, item.Modifiers));
        }

        if (!_shortcuts.TryValidateOverrides(hotkeys, out var hotkeyError))
            throw new InvalidDataException(hotkeyError ?? "Settings transfer contains invalid hotkeys.");

        return new SettingsTransferPreview(
            CurrentFormatVersion,
            new ReadOnlyDictionary<string, string>(portable),
            hotkeys.AsReadOnly(),
            ignoredSettings,
            ignoredHotkeys);
    }

    public void ApplyImport(SettingsTransferPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Version != CurrentFormatVersion || preview.Settings.Count > MaxSettings || preview.Hotkeys.Count > MaxHotkeys)
            throw new InvalidDataException("Settings import preview is invalid.");

        foreach (var item in preview.Settings)
        {
            if (!PortableSettings.TryGetValue(item.Key, out var validator) || !validator(item.Value))
                throw new InvalidDataException($"Portable setting '{item.Key}' has an invalid value.");
        }
        if (!_shortcuts.TryValidateOverrides(preview.Hotkeys, out var hotkeyError))
            throw new InvalidDataException(hotkeyError ?? "Settings transfer contains invalid hotkeys.");

        _settings.ApplyPortableSettings(preview.Settings, preview.Hotkeys);
    }

    internal static bool IsPortableSetting(string key) => PortableSettings.ContainsKey(key);

    private static void EnsureNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Settings transfer repeats JSON property '{property.Name}'.");
                EnsureNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                EnsureNoDuplicateProperties(item);
        }
    }

    private static void WriteAtomically(string path, string json)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException("Settings export destination is invalid.");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup after a failed export.
            }
        }
    }

    private static bool IsBool(string value) => bool.TryParse(value, out _);

    private static bool IsLoupeFactor(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) &&
           factor is 2 or 3 or 4 or 6;

    private static bool IsGalleryTileSize(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) &&
           double.IsFinite(size) && size is >= 80 and <= 320;

    private static bool IsOneOf(string value, params string[] choices)
        => choices.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool IsBoundedText(string? value, int maxLength)
        => value is not null && value.Length <= maxLength && !value.Any(char.IsControl);
}

public sealed record SettingsTransferExportResult(int SettingsCount, int HotkeyCount);

public sealed class SettingsTransferPreview
{
    internal SettingsTransferPreview(
        int version,
        IReadOnlyDictionary<string, string> settings,
        IReadOnlyList<HotkeyOverride> hotkeys,
        int ignoredSettingsCount,
        int ignoredHotkeysCount)
    {
        Version = version;
        Settings = settings;
        Hotkeys = hotkeys;
        IgnoredSettingsCount = ignoredSettingsCount;
        IgnoredHotkeysCount = ignoredHotkeysCount;
    }

    public int Version { get; }
    public IReadOnlyDictionary<string, string> Settings { get; }
    public IReadOnlyList<HotkeyOverride> Hotkeys { get; }
    public int IgnoredSettingsCount { get; }
    public int IgnoredHotkeysCount { get; }
    public int IgnoredCount => IgnoredSettingsCount + IgnoredHotkeysCount;
}

internal sealed record SettingsTransferDocument(
    string Format,
    int Version,
    Dictionary<string, string>? Settings,
    List<SettingsTransferHotkey>? Hotkeys);

internal sealed record SettingsTransferHotkey(string Action, string Key, string Modifiers);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(SettingsTransferDocument))]
internal partial class SettingsTransferJsonContext : JsonSerializerContext;
