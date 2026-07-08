using System.IO;
using Images.Services;
using Microsoft.Data.Sqlite;

namespace Images.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void TouchRecentFolderNormalizesAndDeduplicatesFolders()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var folder = System.IO.Path.Combine(temp.Path, "Photos");
        Directory.CreateDirectory(folder);

        service.TouchRecentFolder(folder);
        service.TouchRecentFolder(folder + System.IO.Path.DirectorySeparatorChar);

        var recent = service.GetRecentFolders();
        var expected = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(folder));
        Assert.Equal([expected], recent);
    }

    [Fact]
    public void TouchRecentFolderSkipsMissingFolders()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));

        service.TouchRecentFolder(System.IO.Path.Combine(temp.Path, "missing"));

        Assert.Empty(service.GetRecentFolders());
    }

    [Fact]
    public void GetRecentFoldersReturnsEmptyForNonPositiveMax()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var folder = System.IO.Path.Combine(temp.Path, "Photos");
        Directory.CreateDirectory(folder);
        service.TouchRecentFolder(folder);

        Assert.Empty(service.GetRecentFolders(0));
        Assert.Empty(service.GetRecentFolders(-5));
    }

    [Fact]
    public void TouchRecentTransferFolderNormalizesDeduplicatesAndOrdersFolders()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var first = System.IO.Path.Combine(temp.Path, "First");
        var second = System.IO.Path.Combine(temp.Path, "Second");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        service.TouchRecentTransferFolder(first);
        service.TouchRecentTransferFolder(second);
        service.TouchRecentTransferFolder(first + System.IO.Path.DirectorySeparatorChar);

        Assert.Equal(
            [
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(first)),
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(second))
            ],
            service.GetRecentTransferFolders());
    }

    [Fact]
    public void GetRecentTransferFoldersFiltersMissingFoldersAndHonorsMax()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var first = System.IO.Path.Combine(temp.Path, "First");
        var second = System.IO.Path.Combine(temp.Path, "Second");
        var missing = System.IO.Path.Combine(temp.Path, "Missing");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        service.SetString(
            Keys.RecentTransferFolders,
            $"[\"{first.Replace("\\", "\\\\")}\",\"{missing.Replace("\\", "\\\\")}\",\"{second.Replace("\\", "\\\\")}\"]");

        Assert.Equal([first], service.GetRecentTransferFolders(1));
        Assert.Equal([first, second], service.GetRecentTransferFolders());
        Assert.Empty(service.GetRecentTransferFolders(0));
    }

    [Fact]
    public void ConfirmRecycleBinDeleteSettingDefaultsOnAndPersistsOptOut()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));

        Assert.True(service.GetBool(Keys.ConfirmRecycleBinDelete, true));

        service.SetBool(Keys.ConfirmRecycleBinDelete, false);

        Assert.False(service.GetBool(Keys.ConfirmRecycleBinDelete, true));
    }

    [Fact]
    public void SettingsIaKeysDefaultAndPersist()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));

        Assert.True(service.GetBool(Keys.RememberWindowPlacement, true));
        Assert.False(service.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.False(service.GetBool(Keys.AccessibilityHighContrast, false));

        service.SetBool(Keys.RememberWindowPlacement, false);
        service.SetBool(Keys.AccessibilityReduceMotion, true);
        service.SetBool(Keys.AccessibilityHighContrast, true);

        Assert.False(service.GetBool(Keys.RememberWindowPlacement, true));
        Assert.True(service.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.True(service.GetBool(Keys.AccessibilityHighContrast, false));
    }

    [Fact]
    public void Constructor_WhenDatabaseIsCorrupt_QuarantinesAndStartsFresh()
    {
        using var temp = TestDirectory.Create();
        var dbPath = Path.Combine(temp.Path, "settings.db");
        File.WriteAllText(dbPath, "not a sqlite database");

        var service = new SettingsService(dbPath);

        service.SetBool(Keys.FilmstripVisible, false);

        Assert.False(service.GetBool(Keys.FilmstripVisible, true));
        Assert.True(File.Exists(dbPath));
        var quarantine = Assert.Single(Directory.GetFiles(temp.Path, "settings.db.corrupt-*"));
        Assert.Contains("not a sqlite database", File.ReadAllText(quarantine));
    }

    [Fact]
    public void Constructor_WhenDatabaseIsNew_MigratesSchemaToCurrentVersion()
    {
        using var temp = TestDirectory.Create();
        var dbPath = Path.Combine(temp.Path, "settings.db");

        _ = new SettingsService(dbPath);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        Assert.Equal(1, ExecuteScalarInt(conn, "PRAGMA user_version;"));
        Assert.Equal(1, ExecuteScalarInt(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"));
        Assert.Equal(1, ExecuteScalarInt(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'recent_folders';"));
        Assert.Equal(1, ExecuteScalarInt(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'hotkeys';"));
    }

    [Fact]
    public void ConnectionString_UsesPrivateCacheForWalConcurrency()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(Path.Combine(temp.Path, "settings.db"));

        var builder = new SqliteConnectionStringBuilder(service.ConnectionStringForTests);

        Assert.Equal(SqliteCacheMode.Private, builder.Cache);
    }

    [Fact]
    public void PrimitiveSettings_RoundTripUsingInvariantStorage()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(Path.Combine(temp.Path, "settings.db"));

        service.SetInt("test.int", 42);
        service.SetDouble("test.double", 12.5);

        Assert.Equal(42, service.GetInt("test.int", 0));
        Assert.Equal(12.5, service.GetDouble("test.double", 0));
        Assert.Equal(7, service.GetInt("missing.int", 7));
        Assert.Equal(3.5, service.GetDouble("missing.double", 3.5));
    }

    [Fact]
    public void HotkeyOverrides_RoundTripAndRemove()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(Path.Combine(temp.Path, "settings.db"));

        service.SetHotkey("open", "O", "Control, Shift");

        var hotkey = Assert.Single(service.GetHotkeys());
        Assert.Equal("open", hotkey.Action);
        Assert.Equal("O", hotkey.Key);
        Assert.Equal("Control, Shift", hotkey.Modifiers);
        Assert.Equal(hotkey, service.GetHotkey("open"));

        service.RemoveHotkey("open");

        Assert.Null(service.GetHotkey("open"));
        Assert.Empty(service.GetHotkeys());
    }

    [Fact]
    public void CreateDefault_WhenStorageRootIsUnavailable_DisablesPersistenceSafely()
    {
        var service = SettingsService.CreateDefault(() => null);

        service.SetBool(Keys.ConfirmRecycleBinDelete, false);

        Assert.True(service.GetBool(Keys.ConfirmRecycleBinDelete, true));
        Assert.Empty(service.GetRecentFolders());
    }

    private static int ExecuteScalarInt(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
