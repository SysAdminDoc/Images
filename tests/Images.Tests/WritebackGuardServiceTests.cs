using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class WritebackGuardServiceTests
{
    [Theory]
    [InlineData("none", WritebackBackupMode.None)]
    [InlineData("same-folder", WritebackBackupMode.SameFolder)]
    [InlineData("app-local", WritebackBackupMode.AppLocal)]
    [InlineData("unknown", WritebackBackupMode.None)]
    public void GetBackupMode_ParsesSettingCorrectly(string settingValue, WritebackBackupMode expected)
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        settings.SetString("writeback.backup-policy", settingValue);

        var mode = WritebackGuardService.GetBackupMode(settings);

        Assert.Equal(expected, mode);
    }

    [Fact]
    public void CreateBackup_SameFolder_CreatesBackupFile()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "image-data");

        var backupPath = WritebackGuardService.CreateBackup(source, WritebackBackupMode.SameFolder);

        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
        Assert.Equal("image-data", File.ReadAllText(backupPath));
        Assert.Contains("_backup_", Path.GetFileName(backupPath));
    }

    [Fact]
    public void CreateBackup_None_ReturnsNull()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "image-data");

        var result = WritebackGuardService.CreateBackup(source, WritebackBackupMode.None);

        Assert.Null(result);
    }

    [Fact]
    public void CreateBackup_MissingFile_ReturnsNull()
    {
        var result = WritebackGuardService.CreateBackup(@"C:\nonexistent\file.jpg", WritebackBackupMode.SameFolder);

        Assert.Null(result);
    }
}
