using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class SettingsTransferServiceTests
{
    [Fact]
    public void Export_ContainsOnlyPortableAllowlistAndKnownHotkeys()
    {
        using var temp = TestDirectory.Create();
        var settings = CreateSettings(temp.Path);
        settings.SetString(Keys.ThemeMode, "latte");
        settings.SetBool(Keys.UpdateCheckEnabled, true);
        settings.SetDouble(Keys.WindowLeft, 123.5);
        settings.SetString(Keys.LastImagePath, @"C:\Private\photo.jpg");
        settings.SetString(Keys.RecentTransferFolders, "[\"C:\\\\Private\"]");
        settings.SetHotkey(CommandIds.Open, "O", "Control, Shift");
        settings.SetHotkey("future-command", "F9", "");

        var path = Path.Combine(temp.Path, "images-settings.json");
        var result = new SettingsTransferService(settings).Export(path);
        var json = File.ReadAllText(path);

        Assert.Equal(1, result.SettingsCount);
        Assert.Equal(1, result.HotkeyCount);
        Assert.Contains(Keys.ThemeMode, json, StringComparison.Ordinal);
        Assert.Contains(CommandIds.Open, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Keys.UpdateCheckEnabled, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Keys.WindowLeft, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Keys.LastImagePath, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Keys.RecentTransferFolders, json, StringComparison.Ordinal);
        Assert.DoesNotContain("future-command", json, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Private", json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(temp.Path, ".*.tmp"));
    }

    [Fact]
    public void PreviewAndApply_IgnoreFutureEntriesAndMergeKnownValues()
    {
        using var temp = TestDirectory.Create();
        var settings = CreateSettings(temp.Path);
        settings.SetString(Keys.ThemeMode, "dark");
        settings.SetBool(Keys.UpdateCheckEnabled, true);
        settings.SetString(Keys.LastImagePath, @"C:\Private\keep.jpg");
        settings.SetHotkey(CommandIds.Next, "N", "Control");

        var path = WriteTransfer(temp.Path, """
            {
              "format": "images-settings",
              "version": 1,
              "settings": {
                "appearance.theme": "latte",
                "accessibility.reduce-motion": "true",
                "future.preference": "kept by a newer version"
              },
              "hotkeys": [
                { "action": "open", "key": "O", "modifiers": "Control, Shift" },
                { "action": "future-command", "key": "F9", "modifiers": "" }
              ]
            }
            """);

        var transfer = new SettingsTransferService(settings);
        var preview = transfer.PreviewImport(path);

        Assert.Equal(2, preview.Settings.Count);
        Assert.Single(preview.Hotkeys);
        Assert.Equal(2, preview.IgnoredCount);

        transfer.ApplyImport(preview);

        Assert.Equal("latte", settings.GetString(Keys.ThemeMode));
        Assert.True(settings.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.Equal("O", settings.GetHotkey(CommandIds.Open)?.Key);
        Assert.Equal("N", settings.GetHotkey(CommandIds.Next)?.Key);
        Assert.True(settings.GetBool(Keys.UpdateCheckEnabled, false));
        Assert.Equal(@"C:\Private\keep.jpg", settings.GetString(Keys.LastImagePath));
    }

    [Fact]
    public void PreviewImport_RejectsInvalidKnownValueAndConflictingHotkey()
    {
        using var temp = TestDirectory.Create();
        var settings = CreateSettings(temp.Path);
        settings.SetString(Keys.ThemeMode, "dark");
        var transfer = new SettingsTransferService(settings);

        var invalidSetting = WriteTransfer(temp.Path, """
            {
              "format": "images-settings",
              "version": 1,
              "settings": { "appearance.theme": "ultraviolet" },
              "hotkeys": []
            }
            """, "invalid-setting.json");
        Assert.Throws<InvalidDataException>(() => transfer.PreviewImport(invalidSetting));

        var conflict = WriteTransfer(temp.Path, """
            {
              "format": "images-settings",
              "version": 1,
              "settings": {},
              "hotkeys": [
                { "action": "paste", "key": "O", "modifiers": "Control" }
              ]
            }
            """, "conflict.json");
        Assert.Throws<InvalidDataException>(() => transfer.PreviewImport(conflict));

        Assert.Equal("dark", settings.GetString(Keys.ThemeMode));
        Assert.Null(settings.GetHotkey(CommandIds.Paste));
    }

    [Fact]
    public void PreviewImport_RejectsDuplicatePropertiesAndOversizedFiles()
    {
        using var temp = TestDirectory.Create();
        var transfer = new SettingsTransferService(CreateSettings(temp.Path));
        var duplicate = WriteTransfer(temp.Path, """
            {
              "format": "images-settings",
              "version": 1,
              "settings": {
                "appearance.theme": "dark",
                "appearance.theme": "latte"
              },
              "hotkeys": []
            }
            """, "duplicate.json");
        Assert.Throws<InvalidDataException>(() => transfer.PreviewImport(duplicate));

        var oversized = Path.Combine(temp.Path, "oversized.json");
        File.WriteAllBytes(oversized, new byte[SettingsTransferService.MaxTransferBytes + 1]);
        Assert.Throws<InvalidDataException>(() => transfer.PreviewImport(oversized));
    }

    [Fact]
    public void ApplyPortableSettings_RollsBackEveryMutationWhenTransactionFails()
    {
        using var temp = TestDirectory.Create();
        var settings = CreateSettings(temp.Path);
        settings.SetString(Keys.ThemeMode, "dark");

        Assert.Throws<IOException>(() => settings.ApplyPortableSettings(
            new Dictionary<string, string>
            {
                [Keys.ThemeMode] = "latte",
                [Keys.AccessibilityReduceMotion] = "true"
            },
            [new HotkeyOverride(CommandIds.Open, "O", "Control, Shift")],
            mutation =>
            {
                if (mutation == 1)
                    throw new IOException("simulated write failure");
            }));

        Assert.Equal("dark", settings.GetString(Keys.ThemeMode));
        Assert.Null(settings.GetString(Keys.AccessibilityReduceMotion));
        Assert.Null(settings.GetHotkey(CommandIds.Open));
    }

    [Fact]
    public void PortableAllowlist_ExplicitlyExcludesPrivateAndDeviceSpecificKeys()
    {
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.UpdateCheckEnabled));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.WindowLeft));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.WindowTop));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.WindowWidth));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.WindowHeight));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.WindowMaximized));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.LastImagePath));
        Assert.False(SettingsTransferService.IsPortableSetting(Keys.RecentTransferFolders));
    }

    private static SettingsService CreateSettings(string root)
        => new(Path.Combine(root, $"settings-{Guid.NewGuid():N}.db"));

    private static string WriteTransfer(string root, string json, string name = "import.json")
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, json);
        return path;
    }
}
