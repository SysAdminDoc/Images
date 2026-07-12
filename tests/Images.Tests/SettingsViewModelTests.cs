using System.IO;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void GeneralAccessibilityAndArchiveSettingsPersistImmediately()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);

        viewModel.RememberWindowPlacement = false;
        viewModel.SelectedTheme = Assert.Single(viewModel.AvailableThemes, theme => theme.Key == "latte");
        viewModel.ReduceMotion = true;
        viewModel.HighContrastMode = true;
        viewModel.ArchiveRightToLeft = true;
        viewModel.ArchiveOldScanFilter = true;
        viewModel.ArchiveSpreadMode = true;

        Assert.False(settings.GetBool(Keys.RememberWindowPlacement, true));
        Assert.Equal("latte", settings.GetString(Keys.ThemeMode, "dark"));
        Assert.True(settings.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.True(settings.GetBool(Keys.AccessibilityHighContrast, false));
        Assert.True(settings.GetBool(Keys.ArchiveRightToLeft, false));
        Assert.True(settings.GetBool(Keys.ArchiveOldScanFilter, false));
        Assert.True(settings.GetBool(Keys.ArchiveSpreadMode, false));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
        Assert.Contains("two-page spreads", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreLastSession_DefaultsOffAndPersistsWhenEnabled()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);

        Assert.False(viewModel.RestoreLastSession);
        Assert.False(settings.GetBool(Keys.RestoreLastSession, false));

        viewModel.RestoreLastSession = true;

        Assert.True(settings.GetBool(Keys.RestoreLastSession, false));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
    }

    [Fact]
    public void HotkeysAndDiagnosticsSummariesExposeExpectedSettingsSections()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);

        Assert.Contains(viewModel.ShortcutRows, r => r.Id == CommandIds.BatchProcessor);
        Assert.Contains("editable shortcuts", viewModel.HotkeySummary, StringComparison.Ordinal);
        Assert.Contains("using their defaults", viewModel.HotkeySummary, StringComparison.Ordinal);
        Assert.Contains("Diagnostics", viewModel.DiagnosticsStorageSummary, StringComparison.Ordinal);
        Assert.Contains("codec report", viewModel.DiagnosticsStorageSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortcutRowsApplyRejectConflictsAndReset()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);
        var open = Assert.Single(viewModel.ShortcutRows, r => r.Id == CommandIds.Open);

        open.ShortcutText = "Ctrl+Shift+O";
        viewModel.ApplyShortcutCommand.Execute(open);

        Assert.Equal("O", settings.GetHotkey(CommandIds.Open)?.Key);
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
        Assert.Contains(viewModel.ShortcutRows, r => r.Id == CommandIds.Open && r.ShortcutText == "Ctrl+Shift+O");
        Assert.Contains("customized", viewModel.HotkeySummary, StringComparison.Ordinal);

        var paste = Assert.Single(viewModel.ShortcutRows, r => r.Id == CommandIds.Paste);
        paste.ShortcutText = "Ctrl+Shift+O";
        viewModel.ApplyShortcutCommand.Execute(paste);

        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Warning, viewModel.SettingsStatusTone);
        Assert.Contains("already uses", viewModel.SettingsStatusText, StringComparison.Ordinal);

        open = Assert.Single(viewModel.ShortcutRows, r => r.Id == CommandIds.Open);
        viewModel.ResetShortcutCommand.Execute(open);

        Assert.Null(settings.GetHotkey(CommandIds.Open));
        Assert.Contains(viewModel.ShortcutRows, r => r.Id == CommandIds.Open && r.ShortcutText == "Ctrl+O");
    }
}
