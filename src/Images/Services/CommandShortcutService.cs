using System.Globalization;
using System.Windows.Input;
using Images.Localization;

namespace Images.Services;

public sealed class CommandShortcutService
{
    private readonly SettingsService _settings;
    private readonly IReadOnlyList<CommandShortcutDefinition> _definitions;
    private readonly IReadOnlyList<CommandShortcutDefinition> _reservedDefinitions;

    public CommandShortcutService(SettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _definitions = CreateDefinitions();
        _reservedDefinitions = CreateReservedDefinitions();
    }

    public IReadOnlyList<CommandShortcutDefinition> Definitions => _definitions;

    public CommandShortcutDefinition GetDefinition(string id)
        => _definitions.First(d => string.Equals(d.Id, id, StringComparison.Ordinal));

    public IReadOnlyDictionary<string, string> GetShortcutTextMap()
        => _definitions.ToDictionary(d => d.Id, d => GetShortcutText(d.Id), StringComparer.Ordinal);

    public IReadOnlyList<CommandShortcutSnapshot> GetSnapshots()
        => _definitions
            .Select(d =>
            {
                var shortcut = GetShortcutText(d.Id);
                return new CommandShortcutSnapshot(
                    d.Id,
                    d.Name,
                    d.Category,
                    shortcut,
                    d.DefaultShortcut,
                    !string.Equals(shortcut, d.DefaultShortcut, StringComparison.Ordinal));
            })
            .ToList();

    public string GetShortcutText(string id)
    {
        if (!TryGetEffectiveGesture(id, out var gesture))
            return string.Empty;

        return gesture.ToDisplayText();
    }

    public bool IsShortcut(string id, Key key, ModifierKeys modifiers)
        => TryGetEffectiveGesture(id, out var gesture)
           && gesture.Equals(ShortcutGesture.FromKeyEvent(key, modifiers));

    public bool TryMatch(Key key, ModifierKeys modifiers, out CommandShortcutDefinition definition)
    {
        var candidate = ShortcutGesture.FromKeyEvent(key, modifiers);
        foreach (var item in _definitions)
        {
            if (TryGetEffectiveGesture(item.Id, out var gesture) && gesture.Equals(candidate))
            {
                definition = item;
                return true;
            }
        }

        definition = default!;
        return false;
    }

    public ShortcutUpdateResult SetShortcut(string id, string shortcutText)
    {
        if (!_definitions.Any(d => string.Equals(d.Id, id, StringComparison.Ordinal)))
            return ShortcutUpdateResult.UnknownCommand();

        if (!ShortcutGesture.TryParse(shortcutText, out var gesture))
            return ShortcutUpdateResult.Invalid();

        foreach (var other in _definitions)
        {
            if (string.Equals(other.Id, id, StringComparison.Ordinal))
                continue;

            if (TryGetEffectiveGesture(other.Id, out var otherGesture) && otherGesture.Equals(gesture))
                return ShortcutUpdateResult.Conflict(other);
        }

        foreach (var reserved in _reservedDefinitions)
        {
            if (ShortcutGesture.TryParse(reserved.DefaultShortcut, out var reservedGesture)
                && reservedGesture.Equals(gesture))
            {
                return ShortcutUpdateResult.Conflict(reserved);
            }
        }

        var definition = GetDefinition(id);
        if (ShortcutGesture.TryParse(definition.DefaultShortcut, out var defaultGesture)
            && defaultGesture.Equals(gesture))
        {
            _settings.RemoveHotkey(id);
            return ShortcutUpdateResult.Reset();
        }

        _settings.SetHotkey(
            id,
            gesture.Key.ToString(),
            gesture.Modifiers == ModifierKeys.None ? string.Empty : gesture.Modifiers.ToString());
        return ShortcutUpdateResult.Saved();
    }

    public void ResetShortcut(string id) => _settings.RemoveHotkey(id);

    internal bool TryValidateOverrides(IReadOnlyList<HotkeyOverride> overrides, out string? error)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        error = null;

        var definitionsById = _definitions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var imported = new Dictionary<string, ShortcutGesture>(StringComparer.Ordinal);
        foreach (var hotkey in overrides)
        {
            if (!definitionsById.ContainsKey(hotkey.Action))
            {
                error = $"Unknown command: {hotkey.Action}";
                return false;
            }

            if (!ShortcutGesture.TryCreate(hotkey.Key, hotkey.Modifiers, out var gesture))
            {
                error = $"Invalid shortcut for {hotkey.Action}.";
                return false;
            }

            if (!imported.TryAdd(hotkey.Action, gesture))
            {
                error = $"Duplicate shortcut entry: {hotkey.Action}";
                return false;
            }
        }

        var actionByGesture = new Dictionary<ShortcutGesture, string>();
        foreach (var definition in _definitions)
        {
            if (!imported.TryGetValue(definition.Id, out var gesture) &&
                !TryGetEffectiveGesture(definition.Id, out gesture))
            {
                continue;
            }

            if (actionByGesture.TryGetValue(gesture, out var conflict))
            {
                error = $"Shortcut conflict between {definition.Id} and {conflict}.";
                return false;
            }

            actionByGesture.Add(gesture, definition.Id);
        }

        foreach (var reserved in _reservedDefinitions)
        {
            if (ShortcutGesture.TryParse(reserved.DefaultShortcut, out var reservedGesture) &&
                actionByGesture.TryGetValue(reservedGesture, out var conflict))
            {
                error = $"Shortcut for {conflict} conflicts with reserved command {reserved.Id}.";
                return false;
            }
        }

        return true;
    }

    private bool TryGetEffectiveGesture(string id, out ShortcutGesture gesture)
    {
        if (_settings.GetHotkey(id) is { } hotkey
            && ShortcutGesture.TryCreate(hotkey.Key, hotkey.Modifiers, out gesture))
        {
            return true;
        }

        var definition = GetDefinition(id);
        return ShortcutGesture.TryParse(definition.DefaultShortcut, out gesture);
    }

    private static IReadOnlyList<CommandShortcutDefinition> CreateDefinitions()
    {
        var nav = Strings.CommandPalette_Category_Navigation;
        var view = Strings.CommandPalette_Category_View;
        var edit = Strings.CommandPalette_Category_Edit;
        var file = Strings.CommandPalette_Category_File;
        var tools = Strings.CommandPalette_Category_Tools;
        var compare = Strings.CommandPalette_Category_Compare;
        var help = Strings.CommandPalette_Category_Help;

        return
        [
            new(CommandIds.Open, Strings.CommandPalette_Open, nav, "Ctrl+O"),
            new(CommandIds.BrowseFolder, Strings.CommandPalette_BrowseFolder, nav, "Ctrl+Shift+F"),
            new(CommandIds.Previous, Strings.CommandPalette_Previous, nav, "Left"),
            new(CommandIds.Next, Strings.CommandPalette_Next, nav, "Right"),
            new(CommandIds.First, Strings.CommandPalette_First, nav, "Home"),
            new(CommandIds.Last, Strings.CommandPalette_Last, nav, "End"),
            new(CommandIds.Refresh, Strings.CommandPalette_Refresh, nav, "F5"),

            new(CommandIds.Filmstrip, Strings.CommandPalette_Filmstrip, view, "T"),
            new(CommandIds.MetadataHud, Strings.CommandPalette_MetadataHud, view, "I"),
            new(CommandIds.ZoomLock, Strings.CommandPalette_ZoomLock, view, ""),
            new(CommandIds.Loupe, Strings.CommandPalette_Loupe, view, "L"),
            new(CommandIds.TransparencyGrid, Strings.CommandPalette_TransparencyGrid, view, ""),
            new(CommandIds.FocusPeaking, Strings.CommandPalette_FocusPeaking, view, "F"),
            new(CommandIds.ExposureClipping, Strings.CommandPalette_ExposureClipping, view, "H"),
            new(CommandIds.Gallery, Strings.CommandPalette_Gallery, view, "G"),
            new(CommandIds.ExtractText, Strings.CommandPalette_ExtractText, view, "E"),

            new(CommandIds.CropMode, Strings.CommandPalette_CropMode, edit, "C"),
            new(CommandIds.SelectionMode, Strings.CommandPalette_SelectionMode, edit, "S"),
            new(CommandIds.Resize, Strings.CommandPalette_Resize, edit, "Ctrl+Alt+R"),
            new(CommandIds.Adjustments, Strings.CommandPalette_Adjustments, edit, "Ctrl+Alt+A"),
            new(CommandIds.Effects, Strings.CommandPalette_Effects, edit, "Ctrl+Alt+F"),
            new(CommandIds.AutoEnhance, Strings.CommandPalette_AutoEnhance, edit, "Ctrl+Alt+E"),
            new(CommandIds.Perspective, Strings.CommandPalette_Perspective, edit, "Ctrl+Alt+P"),
            new(CommandIds.ExposureBrush, Strings.CommandPalette_ExposureBrush, edit, "Ctrl+Alt+D"),
            new(CommandIds.RedEye, Strings.CommandPalette_RedEye, edit, "Ctrl+Alt+Y"),
            new(CommandIds.Retouch, Strings.CommandPalette_Retouch, edit, "Ctrl+Alt+H"),
            new(CommandIds.ExportWorkbench, Strings.CommandPalette_ExportWorkbench, edit, "Ctrl+Alt+W"),

            new(CommandIds.Delete, Strings.CommandPalette_Delete, file, "Del"),
            new(CommandIds.Reload, Strings.CommandPalette_Reload, file, "Ctrl+Shift+R"),
            new(CommandIds.Print, Strings.CommandPalette_Print, file, "Ctrl+P"),
            new(CommandIds.SaveCopy, Strings.CommandPalette_SaveCopy, file, "Ctrl+Shift+S"),
            new(CommandIds.Paste, Strings.CommandPalette_Paste, file, "Ctrl+V"),

            new(CommandIds.ReferenceBoard, Strings.CommandPalette_ReferenceBoard, tools, "Ctrl+B"),
            new(CommandIds.DuplicateCleanup, Strings.CommandPalette_DuplicateCleanup, tools, "Ctrl+Shift+D"),
            new(CommandIds.FileHealthScan, Strings.CommandPalette_FileHealthScan, tools, "Ctrl+Shift+H"),
            new(CommandIds.TagGraph, Strings.CommandPalette_TagGraph, tools, "Ctrl+Shift+T"),
            new(CommandIds.ImportInbox, Strings.CommandPalette_ImportInbox, tools, "Ctrl+Shift+I"),
            new(CommandIds.MacroActions, Strings.CommandPalette_MacroActions, tools, "Ctrl+Shift+M"),
            new(CommandIds.BatchProcessor, Strings.CommandPalette_BatchProcessor, tools, "Ctrl+Shift+B"),
            new(CommandIds.EditStack, Strings.CommandPalette_EditStack, tools, "Ctrl+Shift+E"),

            new(CommandIds.Compare, Strings.CommandPalette_Compare, compare, "Ctrl+Alt+C"),
            new(CommandIds.CompareWith, Strings.CommandPalette_CompareWith, compare, "Ctrl+Alt+V"),

            new(CommandIds.Settings, Strings.CommandPalette_Settings, help, "Ctrl+,"),
            new(CommandIds.CommandPalette, Strings.CommandPalette_ToggleCommandPalette, help, "Ctrl+Shift+P"),
        ];
    }

    private static IReadOnlyList<CommandShortcutDefinition> CreateReservedDefinitions()
        =>
        [
            new("reserved.escape", "Dismiss overlays", "Reserved", "Esc"),
            new("reserved.enter", "Apply active edit", "Reserved", "Enter"),
            new("reserved.fullscreen", "Toggle fullscreen", "Reserved", "F11"),
            new("reserved.cheatsheet", "Toggle shortcut help", "Reserved", "/"),
            new("reserved.overlay-exit", "Exit overlay mode", "Reserved", "Ctrl+Alt+O"),
            new("reserved.zoom-mode", "Cycle zoom mode", "Reserved", "Ctrl+F"),
            new("reserved.back", "Previous image with Backspace", "Reserved", "Back"),
            new("reserved.space", "Next image with Space", "Reserved", "Space"),
            new("reserved.zoom-in", "Zoom in", "Reserved", "Plus"),
            new("reserved.zoom-out", "Zoom out", "Reserved", "Minus"),
            new("reserved.fit", "Fit to viewport", "Reserved", "D0"),
            new("reserved.one-to-one", "One-to-one zoom", "Reserved", "D1"),
        ];
}

public static class CommandIds
{
    public const string Open = "open";
    public const string BrowseFolder = "browse-folder";
    public const string Previous = "previous";
    public const string Next = "next";
    public const string First = "first";
    public const string Last = "last";
    public const string Refresh = "refresh";
    public const string Filmstrip = "filmstrip";
    public const string MetadataHud = "metadata-hud";
    public const string ZoomLock = "zoom-lock";
    public const string Loupe = "loupe";
    public const string TransparencyGrid = "transparency-grid";
    public const string FocusPeaking = "focus-peaking";
    public const string ExposureClipping = "exposure-clipping";
    public const string Gallery = "gallery";
    public const string ExtractText = "extract-text";
    public const string CropMode = "crop-mode";
    public const string SelectionMode = "selection-mode";
    public const string Resize = "resize";
    public const string Adjustments = "adjustments";
    public const string Effects = "effects";
    public const string AutoEnhance = "auto-enhance";
    public const string Perspective = "perspective";
    public const string ExposureBrush = "exposure-brush";
    public const string RedEye = "red-eye";
    public const string Retouch = "retouch";
    public const string ExportWorkbench = "export-workbench";
    public const string Delete = "delete";
    public const string Reload = "reload";
    public const string Print = "print";
    public const string SaveCopy = "save-copy";
    public const string Paste = "paste";
    public const string ReferenceBoard = "reference-board";
    public const string DuplicateCleanup = "duplicate-cleanup";
    public const string FileHealthScan = "file-health-scan";
    public const string TagGraph = "tag-graph";
    public const string ImportInbox = "import-inbox";
    public const string MacroActions = "macro-actions";
    public const string BatchProcessor = "batch-processor";
    public const string EditStack = "edit-stack";
    public const string Compare = "compare";
    public const string CompareWith = "compare-with";
    public const string Settings = "settings";
    public const string CommandPalette = "command-palette";
}

public sealed record CommandShortcutDefinition(string Id, string Name, string Category, string DefaultShortcut);

public sealed record CommandShortcutSnapshot(
    string Id,
    string Name,
    string Category,
    string Shortcut,
    string DefaultShortcut,
    bool IsCustomized);

public sealed record ShortcutUpdateResult(ShortcutUpdateResultKind Kind, CommandShortcutDefinition? ConflictDefinition = null)
{
    public static ShortcutUpdateResult Saved() => new(ShortcutUpdateResultKind.Saved);
    public static ShortcutUpdateResult Reset() => new(ShortcutUpdateResultKind.Reset);
    public static ShortcutUpdateResult Invalid() => new(ShortcutUpdateResultKind.Invalid);
    public static ShortcutUpdateResult UnknownCommand() => new(ShortcutUpdateResultKind.UnknownCommand);
    public static ShortcutUpdateResult Conflict(CommandShortcutDefinition definition) =>
        new(ShortcutUpdateResultKind.Conflict, definition);
}

public enum ShortcutUpdateResultKind
{
    Saved,
    Reset,
    Invalid,
    Conflict,
    UnknownCommand,
}

public readonly record struct ShortcutGesture(Key Key, ModifierKeys Modifiers)
{
    private static readonly ModifierKeys AllowedModifiers =
        ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt;

    public static ShortcutGesture FromKeyEvent(Key key, ModifierKeys modifiers)
        => new(NormalizeKey(key), modifiers & AllowedModifiers);

    public static bool TryCreate(string keyText, string modifiersText, out ShortcutGesture gesture)
    {
        gesture = default;
        if (!TryParseKey(keyText, out var key))
            return false;

        if (!TryParseModifiers(modifiersText, out var modifiers))
            return false;

        gesture = new ShortcutGesture(key, modifiers);
        return true;
    }

    public static bool TryParse(string? text, out ShortcutGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var modifiers = ModifierKeys.None;
        Key? key = null;
        foreach (var part in parts)
        {
            if (TryParseModifier(part, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (key is not null || !TryParseKey(part, out var parsedKey))
                return false;

            key = parsedKey;
        }

        if (key is null)
            return false;

        gesture = new ShortcutGesture(key.Value, modifiers & AllowedModifiers);
        return true;
    }

    public string ToDisplayText()
    {
        var parts = new List<string>(4);
        if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control) parts.Add("Ctrl");
        if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) parts.Add("Shift");
        if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) parts.Add("Alt");
        parts.Add(FormatKey(Key));
        return string.Join("+", parts);
    }

    private static bool TryParseModifiers(string text, out ModifierKeys modifiers)
    {
        modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(text) || text.Equals("None", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = text.Replace(",", "+", StringComparison.Ordinal);
        foreach (var part in normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseModifier(part, out var modifier))
                return false;
            modifiers |= modifier;
        }

        modifiers &= AllowedModifiers;
        return true;
    }

    private static bool TryParseModifier(string text, out ModifierKeys modifier)
    {
        modifier = text.Trim().ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => ModifierKeys.Control,
            "SHIFT" => ModifierKeys.Shift,
            "ALT" => ModifierKeys.Alt,
            _ => ModifierKeys.None
        };

        return modifier != ModifierKeys.None;
    }

    private static bool TryParseKey(string text, out Key key)
    {
        key = text.Trim().ToUpperInvariant() switch
        {
            "," => Key.OemComma,
            "." => Key.OemPeriod,
            "/" or "?" => Key.OemQuestion,
            ";" => Key.OemSemicolon,
            "[" => Key.OemOpenBrackets,
            "]" => Key.OemCloseBrackets,
            "DEL" => Key.Delete,
            "ESC" => Key.Escape,
            "PLUS" => Key.OemPlus,
            "MINUS" => Key.OemMinus,
            _ => Key.None
        };
        if (key != Key.None)
            return true;

        return Enum.TryParse(text, ignoreCase: true, out key) && key != Key.None;
    }

    private static string FormatKey(Key key)
        => key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.Delete => "Del",
            Key.Escape => "Esc",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            _ => key.ToString()
        };

    private static Key NormalizeKey(Key key)
        => key switch
        {
            Key.LineFeed => Key.Enter,
            _ => key
        };

    public override string ToString() => ToDisplayText();
}
