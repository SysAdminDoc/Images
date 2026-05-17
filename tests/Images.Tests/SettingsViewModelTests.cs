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
        viewModel.ReduceMotion = true;
        viewModel.HighContrastMode = true;
        viewModel.ArchiveRightToLeft = true;
        viewModel.ArchiveOldScanFilter = true;
        viewModel.ArchiveSpreadMode = true;

        Assert.False(settings.GetBool(Keys.RememberWindowPlacement, true));
        Assert.True(settings.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.True(settings.GetBool(Keys.AccessibilityHighContrast, false));
        Assert.True(settings.GetBool(Keys.ArchiveRightToLeft, false));
        Assert.True(settings.GetBool(Keys.ArchiveOldScanFilter, false));
        Assert.True(settings.GetBool(Keys.ArchiveSpreadMode, false));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
        Assert.Contains("two-page spreads", viewModel.SettingsStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeysAndDiagnosticsSummariesExposeExpectedSettingsSections()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);

        Assert.Contains("Ctrl+Shift+B", viewModel.HotkeySummary, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+P", viewModel.HotkeySummary, StringComparison.Ordinal);
        Assert.Contains("Diagnostics", viewModel.DiagnosticsStorageSummary, StringComparison.Ordinal);
        Assert.Contains("codec report", viewModel.DiagnosticsStorageSummary, StringComparison.Ordinal);
    }
}
