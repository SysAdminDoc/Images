using System.IO;
using System.Windows.Input;
using Images.Services;

namespace Images.Tests;

public sealed class CommandShortcutServiceTests
{
    [Fact]
    public void SetShortcut_PersistsOverrideAndUpdatesMatching()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var shortcuts = new CommandShortcutService(settings);

        var result = shortcuts.SetShortcut(CommandIds.Open, "Ctrl+Shift+O");

        Assert.Equal(ShortcutUpdateResultKind.Saved, result.Kind);
        Assert.Equal("Ctrl+Shift+O", shortcuts.GetShortcutText(CommandIds.Open));
        Assert.True(shortcuts.IsShortcut(CommandIds.Open, Key.O, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Equal("O", settings.GetHotkey(CommandIds.Open)?.Key);
    }

    [Fact]
    public void SetShortcut_RejectsCommandAndReservedConflicts()
    {
        using var temp = TestDirectory.Create();
        var shortcuts = new CommandShortcutService(new SettingsService(Path.Combine(temp.Path, "settings.db")));

        var commandConflict = shortcuts.SetShortcut(CommandIds.Paste, "Ctrl+O");
        var reservedConflict = shortcuts.SetShortcut(CommandIds.Open, "F11");

        Assert.Equal(ShortcutUpdateResultKind.Conflict, commandConflict.Kind);
        Assert.Equal(CommandIds.Open, commandConflict.ConflictDefinition?.Id);
        Assert.Equal(ShortcutUpdateResultKind.Conflict, reservedConflict.Kind);
        Assert.StartsWith("reserved.", reservedConflict.ConflictDefinition?.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetShortcut_RemovesOverrideAndRestoresDefault()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var shortcuts = new CommandShortcutService(settings);

        shortcuts.SetShortcut(CommandIds.BatchProcessor, "Ctrl+Alt+B");
        shortcuts.ResetShortcut(CommandIds.BatchProcessor);

        Assert.Null(settings.GetHotkey(CommandIds.BatchProcessor));
        Assert.Equal("Ctrl+Shift+B", shortcuts.GetShortcutText(CommandIds.BatchProcessor));
    }
}
