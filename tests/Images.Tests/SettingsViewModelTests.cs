using System.IO;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

[Collection("WpfSmoke")]
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
        viewModel.ArchiveContinuousMode = true;

        Assert.False(settings.GetBool(Keys.RememberWindowPlacement, true));
        Assert.Equal("latte", settings.GetString(Keys.ThemeMode, "dark"));
        Assert.True(settings.GetBool(Keys.AccessibilityReduceMotion, false));
        Assert.True(settings.GetBool(Keys.AccessibilityHighContrast, false));
        Assert.True(settings.GetBool(Keys.ArchiveRightToLeft, false));
        Assert.True(settings.GetBool(Keys.ArchiveOldScanFilter, false));
        Assert.False(settings.GetBool(Keys.ArchiveSpreadMode, true));
        Assert.True(settings.GetBool(Keys.ArchiveContinuousMode, false));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
        Assert.Contains("Continuous archive reading", viewModel.SettingsStatusText, StringComparison.Ordinal);

        viewModel.ArchiveSpreadMode = true;
        Assert.True(settings.GetBool(Keys.ArchiveSpreadMode, false));
        Assert.False(settings.GetBool(Keys.ArchiveContinuousMode, true));
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
    public void LoupeMagnification_DefaultsTo2xAndPersistsSelection()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);

        Assert.Equal(2.0, viewModel.SelectedLoupeMagnification.Factor, 3);

        var fourX = Assert.Single(viewModel.AvailableLoupeMagnifications, option => option.Factor == 4.0);
        viewModel.SelectedLoupeMagnification = fourX;

        Assert.Equal(4.0, settings.GetDouble(Keys.LoupeFactor, 2.0), 3);
        Assert.Equal(4.0, new SettingsViewModel(settings).SelectedLoupeMagnification.Factor, 3);
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
    }

    [Fact]
    public void HdrToneMapOperator_DefaultsToReinhardAndPersistsSelection()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        var viewModel = new SettingsViewModel(settings);
        var previous = ImageLoader.HdrToneMapOperator;

        try
        {
            Assert.Equal(ToneMapOperator.Reinhard, viewModel.SelectedToneMapOperator.Operator);

            var hable = Assert.Single(viewModel.AvailableToneMapOperators, option => option.Operator == ToneMapOperator.Hable);
            viewModel.SelectedToneMapOperator = hable;

            Assert.Equal("Hable", settings.GetString(Keys.HdrToneMapOperator));
            Assert.Equal(ToneMapOperator.Hable, new SettingsViewModel(settings).SelectedToneMapOperator.Operator);
            Assert.Equal(ToneMapOperator.Hable, ImageLoader.HdrToneMapOperator);
            Assert.Contains("Hable", viewModel.SettingsStatusText, StringComparison.Ordinal);
        }
        finally
        {
            ImageLoader.HdrToneMapOperator = previous;
        }
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

    [Fact]
    public void LocalStorageRegistryReportsRealStoresAndConfirmedResetPreservesModels()
    {
        using var temp = TestDirectory.Create();
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));
        File.WriteAllText(Path.Combine(temp.Path, "catalog.db"), "catalog");
        File.WriteAllText(Path.Combine(temp.Path, "semantic-index.db"), "semantic");
        var models = Directory.CreateDirectory(Path.Combine(temp.Path, "models"));
        File.WriteAllText(Path.Combine(models.FullName, "model.onnx"), "model");
        var prompts = new List<string>();
        var viewModel = new SettingsViewModel(
            settings,
            new LocalDataStoreRegistry(() => temp.Path),
            prompt => { prompts.Add(prompt); return true; });

        Assert.Contains("Catalog:", viewModel.StorageDetail, StringComparison.Ordinal);
        Assert.Contains("Semantic index:", viewModel.StorageDetail, StringComparison.Ordinal);
        Assert.Contains("Local models:", viewModel.StorageDetail, StringComparison.Ordinal);

        viewModel.ClearDerivedDataCommand.Execute(null);

        Assert.Single(prompts);
        Assert.False(File.Exists(Path.Combine(temp.Path, "catalog.db")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "semantic-index.db")));
        Assert.True(File.Exists(Path.Combine(models.FullName, "model.onnx")));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, viewModel.SettingsStatusTone);
    }

    [Fact]
    public void SettingsTransfer_PreviewsAppliesAndRefreshesPortableState()
    {
        using var temp = TestDirectory.Create();
        var source = new SettingsService(Path.Combine(temp.Path, "source.db"));
        source.SetString(Keys.ThemeMode, "latte");
        source.SetBool(Keys.AccessibilityReduceMotion, true);
        source.SetHotkey(CommandIds.Open, "O", "Control, Shift");
        var exportPath = Path.Combine(temp.Path, "images-settings.json");
        var sourceViewModel = new SettingsViewModel(source);

        sourceViewModel.ExportPortableSettings(exportPath);

        Assert.True(File.Exists(exportPath));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, sourceViewModel.SettingsStatusTone);

        var destination = new SettingsService(Path.Combine(temp.Path, "destination.db"));
        destination.SetBool(Keys.UpdateCheckEnabled, true);
        destination.SetString(Keys.LastImagePath, @"C:\Private\unchanged.jpg");
        var destinationViewModel = new SettingsViewModel(destination);
        var preview = destinationViewModel.PreviewPortableSettingsImport(exportPath);

        Assert.NotNull(preview);
        Assert.Contains("2 portable settings", SettingsViewModel.BuildPortableSettingsImportPreview(preview), StringComparison.Ordinal);

        destinationViewModel.ApplyPortableSettingsImport(preview);

        Assert.Equal("latte", destinationViewModel.SelectedTheme.Key);
        Assert.True(destinationViewModel.ReduceMotion);
        Assert.Contains(destinationViewModel.ShortcutRows,
            row => row.Id == CommandIds.Open && row.ShortcutText == "Ctrl+Shift+O");
        Assert.True(destination.GetBool(Keys.UpdateCheckEnabled, false));
        Assert.Equal(@"C:\Private\unchanged.jpg", destination.GetString(Keys.LastImagePath));
        Assert.Equal(SettingsViewModel.SettingsStatusToneKind.Success, destinationViewModel.SettingsStatusTone);
    }
}
