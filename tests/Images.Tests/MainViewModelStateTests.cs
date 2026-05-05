using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class MainViewModelStateTests
{
    [Fact]
    public void OpenFile_PopulatesFolderPreviewAndSortState()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var image10 = WritePng(temp.Path, "image10.png");
            var image2 = WritePng(temp.Path, "image2.png");
            var image1 = WritePng(temp.Path, "image1.png");
            using var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(image10);

            Assert.Equal(image10, viewModel.CurrentPath);
            Assert.Equal("3 / 3", viewModel.PositionText);
            Assert.True(viewModel.ShowFilmstrip);
            Assert.Equal(DirectorySortMode.NaturalName, viewModel.CurrentSortMode);
            Assert.Equal("Sort: Name", viewModel.FolderSortLabel);
            Assert.Equal([image1, image2, image10], viewModel.FolderPreviewItems.Select(i => i.Path));
            Assert.True(viewModel.FolderPreviewItems.Single(i => i.Path == image10).IsCurrent);

            viewModel.SetFolderSortCommand.Execute(DirectorySortMode.NameDescending);

            Assert.Equal(DirectorySortMode.NameDescending, viewModel.CurrentSortMode);
            Assert.Equal("Sort: Z to A", viewModel.FolderSortLabel);
            Assert.Equal("1 / 3", viewModel.PositionText);
            Assert.Equal([image10, image2, image1], viewModel.FolderPreviewItems.Select(i => i.Path));
            Assert.True(viewModel.FolderPreviewItems[0].IsCurrent);
        });
    }

    [Fact]
    public void ToggleFilmstrip_PersistsPreferenceAndSwitchesPreviewSurface()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "a.png");
            WritePng(temp.Path, "b.png");
            var settings = CreateSettings(temp);
            using var viewModel = new MainViewModel(settings);

            viewModel.OpenFile(first);
            Assert.True(viewModel.ShowFilmstrip);
            Assert.False(viewModel.ShowSideFolderPreview);

            viewModel.ToggleFilmstripCommand.Execute(null);

            Assert.False(settings.GetBool(Keys.FilmstripVisible, true));
            Assert.False(viewModel.ShowFilmstrip);
            Assert.True(viewModel.ShowSideFolderPreview);
            Assert.Equal("Filmstrip hidden", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void PasteFromClipboardCommand_SavesImageAndOpensIt()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var source = new FakeClipboardDataSource { Image = CreateBitmap() };
            var clipboardImport = new ClipboardImportService(
                source,
                () => temp.Path,
                () => new DateTimeOffset(2026, 5, 5, 12, 34, 56, 789, TimeSpan.Zero),
                () => Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
            using var viewModel = CreateViewModel(temp, clipboardImport);

            viewModel.PasteFromClipboardCommand.Execute(null);

            var expected = Path.Combine(temp.Path, "clipboard-20260505-123456789-00112233445566778899aabbccddeeff.png");
            Assert.Equal(expected, viewModel.CurrentPath);
            Assert.True(File.Exists(expected));
            Assert.True(viewModel.HasDisplayImage);
            Assert.False(viewModel.HasSecondaryStatus);
            Assert.Equal("Pasted from clipboard", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void PasteFromClipboardCommand_WhenFileListUnsupported_ShowsPersistentStatus()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var textFile = Path.Combine(temp.Path, "notes.txt");
            File.WriteAllText(textFile, "not an image");
            var source = new FakeClipboardDataSource { Files = [textFile] };
            var clipboardImport = new ClipboardImportService(
                source,
                () => temp.Path,
                () => DateTimeOffset.UtcNow,
                () => Guid.NewGuid());
            using var viewModel = CreateViewModel(temp, clipboardImport);

            viewModel.PasteFromClipboardCommand.Execute(null);

            Assert.True(viewModel.HasSecondaryStatus);
            Assert.Equal("Clipboard file not supported", viewModel.SecondaryStatusTitle);
            Assert.Contains("none are formats Images can open", viewModel.SecondaryStatusDetail);
            Assert.Equal(MainViewModel.SecondaryStatusToneKind.Warning, viewModel.SecondaryStatusTone);
            Assert.Equal("No supported image in the clipboard file list", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void OpenRecentFolderCommand_WhenFolderMissing_ShowsRecoveryStatusAndRefreshesList()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var staleFolder = Path.Combine(temp.Path, "stale");
            Directory.CreateDirectory(staleFolder);
            var settings = CreateSettings(temp);
            settings.TouchRecentFolder(staleFolder);
            using var viewModel = new MainViewModel(settings);
            Assert.Contains(staleFolder, viewModel.RecentFolders);
            Directory.Delete(staleFolder);

            viewModel.OpenRecentFolderCommand.Execute(staleFolder);

            Assert.True(viewModel.HasSecondaryStatus);
            Assert.Equal("Recent folder removed", viewModel.SecondaryStatusTitle);
            Assert.Contains("folder no longer exists", viewModel.SecondaryStatusDetail);
            Assert.Equal(MainViewModel.SecondaryStatusToneKind.Warning, viewModel.SecondaryStatusTone);
            Assert.DoesNotContain(staleFolder, viewModel.RecentFolders);
            Assert.Equal("Folder no longer exists", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void OpenRecentFolderCommand_WhenFolderHasNoImages_ShowsEmptyFolderStatus()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var emptyFolder = Path.Combine(temp.Path, "empty");
            Directory.CreateDirectory(emptyFolder);
            using var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenRecentFolderCommand.Execute(emptyFolder);

            Assert.True(viewModel.HasSecondaryStatus);
            Assert.Equal("No supported images in this folder", viewModel.SecondaryStatusTitle);
            Assert.Contains("Choose another folder or paste an image", viewModel.SecondaryStatusDetail);
            Assert.Equal(MainViewModel.SecondaryStatusToneKind.Info, viewModel.SecondaryStatusTone);
            Assert.Equal("No images in empty", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void DeleteCommand_WhenConfirmationDisabled_SkipsDialogAndAdvances()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "a.png");
            var second = WritePng(temp.Path, "b.png");
            var settings = CreateSettings(temp);
            settings.SetBool(Keys.ConfirmRecycleBinDelete, false);
            var deleted = new List<string>();
            var deleteService = new RecycleBinDeleteService(
                settings,
                sendToRecycleBin: path =>
                {
                    deleted.Add(path);
                },
                confirmRecycleBinMove: (_, _) => throw new InvalidOperationException("Confirmation dialog should not be shown."));
            using var viewModel = new MainViewModel(
                settings,
                recycleBinDelete: deleteService);

            viewModel.OpenFile(first);
            viewModel.DeleteCommand.Execute(null);

            Assert.Equal([first], deleted);
            Assert.Equal(second, viewModel.CurrentPath);
            Assert.Equal("1 / 1", viewModel.PositionText);
            Assert.Equal("Sent to Recycle Bin: a.png", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void DeleteCommand_WhenUserOptsOut_PersistsConfirmationPreference()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "a.png");
            WritePng(temp.Path, "b.png");
            var settings = CreateSettings(temp);
            var confirmedPath = "";
            var deleteService = new RecycleBinDeleteService(
                settings,
                sendToRecycleBin: _ => { },
                confirmRecycleBinMove: (_, path) =>
                {
                    confirmedPath = path;
                    return new ConfirmDialog.ConfirmationResult(Confirmed: true, DoNotAskAgain: true);
                });
            using var viewModel = new MainViewModel(
                settings,
                recycleBinDelete: deleteService);

            viewModel.OpenFile(first);
            viewModel.DeleteCommand.Execute(null);

            Assert.Equal(first, confirmedPath);
            Assert.False(settings.GetBool(Keys.ConfirmRecycleBinDelete, true));
        });
    }

    [Fact]
    public void FlushPendingRename_WhenExtensionUnsupported_DoesNotMoveSource()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var source = WritePng(temp.Path, "photo.png");
            var unsupportedTarget = Path.Combine(temp.Path, "photo.txt");
            using var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(source);
            viewModel.IsExtensionUnlocked = true;
            viewModel.Extension = ".txt";
            viewModel.FlushPendingRename();

            Assert.Equal(source, viewModel.CurrentPath);
            Assert.True(File.Exists(source));
            Assert.False(File.Exists(unsupportedTarget));
            Assert.Equal(MainViewModel.RenameStatusKind.Error, viewModel.RenameStatus);
            Assert.Equal("Rename failed: Extension '.txt' is not supported by Images.", viewModel.ToastMessage);
            Assert.Equal("Choose a supported Images extension", viewModel.RenamePreview);
        });
    }

    [Fact]
    public void EditableStemChange_CommitsRenameAfterDebounce()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var source = WritePng(temp.Path, "photo.png");
            var target = Path.Combine(temp.Path, "renamed.png");
            using var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(source);
            viewModel.EditableStem = "renamed";

            Assert.Equal(MainViewModel.RenameStatusKind.Pending, viewModel.RenameStatus);
            Assert.Equal("→ renamed.png", viewModel.RenamePreview);
            Assert.True(File.Exists(source));
            Assert.False(File.Exists(target));

            PumpUntil(() => string.Equals(viewModel.CurrentPath, target, StringComparison.OrdinalIgnoreCase));

            Assert.False(File.Exists(source));
            Assert.True(File.Exists(target));
            Assert.Equal(MainViewModel.RenameStatusKind.Saved, viewModel.RenameStatus);
            Assert.Equal("", viewModel.RenamePreview);
            Assert.Equal("renamed.png", viewModel.CurrentFileName);
        });
    }

    [Fact]
    public void ImageCommands_AreDisabledUntilImageIsLoaded()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var image = WritePng(temp.Path, "photo.png");
            using var viewModel = CreateViewModelWithFastPreview(temp);

            Assert.False(viewModel.DeleteCommand.CanExecute(null));
            Assert.False(viewModel.ReloadCommand.CanExecute(null));
            Assert.False(viewModel.RefreshCommand.CanExecute(null));
            Assert.False(viewModel.ExtractTextCommand.CanExecute(null));
            Assert.False(viewModel.SaveAsCopyCommand.CanExecute(null));
            Assert.False(viewModel.ToggleMetadataHudCommand.CanExecute(null));

            viewModel.OpenFile(image);

            Assert.True(viewModel.HasImage);
            Assert.True(viewModel.HasDisplayImage);
            Assert.True(viewModel.DeleteCommand.CanExecute(null));
            Assert.True(viewModel.ReloadCommand.CanExecute(null));
            Assert.True(viewModel.RefreshCommand.CanExecute(null));
            Assert.True(viewModel.ExtractTextCommand.CanExecute(null));
            Assert.True(viewModel.SaveAsCopyCommand.CanExecute(null));
            Assert.True(viewModel.ToggleMetadataHudCommand.CanExecute(null));

            File.Delete(image);

            Assert.False(viewModel.HasImage);
            Assert.True(viewModel.RefreshCommand.CanExecute(null));
            Assert.False(viewModel.ReloadCommand.CanExecute(null));
        });
    }

    [Fact]
    public void ReloadCommand_ShowsOperationStatusAndDisablesMutatingCommands()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var image = WritePng(temp.Path, "photo.png");
            var viewModel = CreateViewModelWithFastPreview(temp);
            var changed = new List<string>();

            viewModel.OpenFile(image);
            viewModel.PropertyChanged += RecordChangedProperty(changed);

            viewModel.ReloadCommand.Execute(null);

            Assert.True(viewModel.IsOperationBusy);
            Assert.True(viewModel.ShowOperationStatus);
            Assert.Equal("Reloading image", viewModel.OperationStatusTitle);
            Assert.Equal("Refreshing decoder output and metadata.", viewModel.OperationStatusDetail);
            Assert.False(viewModel.ReloadCommand.CanExecute(null));
            Assert.False(viewModel.SaveAsCopyCommand.CanExecute(null));
            Assert.False(viewModel.DeleteCommand.CanExecute(null));
            Assert.False(viewModel.RefreshCommand.CanExecute(null));

            PumpUntil(() => !viewModel.IsOperationBusy);

            Assert.False(viewModel.ShowOperationStatus);
            Assert.Equal("", viewModel.OperationStatusTitle);
            Assert.Equal("", viewModel.OperationStatusDetail);
            Assert.Contains(nameof(MainViewModel.IsOperationBusy), changed);
            Assert.Contains(nameof(MainViewModel.ShowOperationStatus), changed);
        });
    }

    [Fact]
    public void MultiPageNavigation_ShowsOperationStatusAndDisablesPageTurns()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var image = WriteTwoPageTiff(temp.Path, "document.tif");
            var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(image);

            Assert.True(viewModel.HasMultiplePages);
            Assert.Equal("Page 1 / 2", viewModel.PagePositionText);

            viewModel.NextPageCommand.Execute(null);

            Assert.True(viewModel.IsOperationBusy);
            Assert.True(viewModel.ShowOperationStatus);
            Assert.Equal("Loading next page", viewModel.OperationStatusTitle);
            Assert.Equal("Page 2 of 2.", viewModel.OperationStatusDetail);
            Assert.False(viewModel.NextPageCommand.CanExecute(null));
            Assert.False(viewModel.PrevPageCommand.CanExecute(null));

            PumpUntil(() => !viewModel.IsOperationBusy);

            Assert.Equal("Page 2 / 2", viewModel.PagePositionText);
            Assert.True(viewModel.HasPreviousPage);
            Assert.False(viewModel.HasNextPage);
        });
    }

    [Fact]
    public void PageNumber_WhenSetFromScrubber_LoadsRequestedPage()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var image = WriteTwoPageTiff(temp.Path, "document.tif");
            var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(image);

            Assert.True(viewModel.HasMultiplePages);
            Assert.Equal(1, viewModel.PageNumber);

            viewModel.PageNumber = 2;

            Assert.True(viewModel.IsOperationBusy);
            Assert.Equal("Loading page", viewModel.OperationStatusTitle);
            Assert.Equal("Page 2 of 2.", viewModel.OperationStatusDetail);

            PumpUntil(() => !viewModel.IsOperationBusy);

            Assert.Equal(2, viewModel.PageNumber);
            Assert.Equal("Page 2 / 2", viewModel.PagePositionText);
        });
    }

    [Fact]
    public void OpenFile_WhenArchiveWasPartlyRead_ResumesLastPage()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var archive = WriteTwoPageCbz(temp.Path, "book.cbz");
            var settings = CreateSettings(temp);

            using (var viewModel = new MainViewModel(settings))
            {
                viewModel.OpenFile(archive);
                Assert.True(viewModel.IsArchiveBook);
                Assert.Equal("Reading book.cbz \u00B7 Page 1 / 2", viewModel.CurrentArchiveProgressText);
                Assert.Single(viewModel.RecentArchiveBooks);
                viewModel.PageNumber = 2;
                PumpUntil(() => !viewModel.IsOperationBusy);
                Assert.Equal(2, viewModel.PageNumber);
                Assert.Equal("Reading book.cbz \u00B7 Page 2 / 2", viewModel.CurrentArchiveProgressText);
                Assert.Equal("Page 2 / 2", Assert.Single(viewModel.RecentArchiveBooks).ProgressText);
            }

            using var resumed = new MainViewModel(settings);

            Assert.Equal(archive, Assert.Single(resumed.RecentArchiveBooks).Path);

            resumed.OpenFile(archive);

            Assert.Equal(2, resumed.PageNumber);
            Assert.Equal("Page 2 / 2", resumed.PagePositionText);
            Assert.Equal("Continued at page 2", resumed.ToastMessage);
        });
    }

    [Fact]
    public void ArchiveRightToLeft_SwapsPhysicalPageTurnZonesAndPersists()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var archive = WriteTwoPageCbz(temp.Path, "manga.cbz");
            var settings = CreateSettings(temp);
            using var viewModel = new MainViewModel(settings);

            viewModel.OpenFile(archive);

            Assert.True(viewModel.IsArchiveBook);
            Assert.False(viewModel.ArchiveRightToLeft);
            Assert.False(viewModel.LeftBookPageTurnCommand.CanExecute(null));
            Assert.True(viewModel.RightBookPageTurnCommand.CanExecute(null));
            Assert.Equal("Previous book page", viewModel.LeftBookPageTurnTooltip);
            Assert.Equal("Next book page", viewModel.RightBookPageTurnTooltip);

            viewModel.ArchiveRightToLeft = true;

            Assert.True(settings.GetBool(Keys.ArchiveRightToLeft, false));
            Assert.Equal("Right-to-left page turns", viewModel.ArchivePageTurnModeText);
            Assert.Equal("Next book page", viewModel.LeftBookPageTurnTooltip);
            Assert.Equal("Previous book page", viewModel.RightBookPageTurnTooltip);
            Assert.True(viewModel.LeftBookPageTurnCommand.CanExecute(null));
            Assert.False(viewModel.RightBookPageTurnCommand.CanExecute(null));

            viewModel.LeftBookPageTurnCommand.Execute(null);
            PumpUntil(() => !viewModel.IsOperationBusy);

            Assert.Equal(2, viewModel.PageNumber);
            Assert.False(viewModel.LeftBookPageTurnCommand.CanExecute(null));
            Assert.True(viewModel.RightBookPageTurnCommand.CanExecute(null));

            viewModel.RightBookPageTurnCommand.Execute(null);
            PumpUntil(() => !viewModel.IsOperationBusy);

            Assert.Equal(1, viewModel.PageNumber);
        });
    }

    [Fact]
    public void FirstRunGuidance_ExposesCapabilityAndPrivacySummaries()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var settings = CreateSettings(temp);
            using var viewModel = new MainViewModel(settings);

            Assert.Contains("No telemetry", viewModel.FirstRunPrivacyText, StringComparison.Ordinal);
            Assert.Contains("open extensions", viewModel.FirstRunFormatStatusText, StringComparison.Ordinal);
            Assert.Contains("export extensions", viewModel.FirstRunFormatStatusText, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.FirstRunOcrStatusText));
            Assert.False(string.IsNullOrWhiteSpace(viewModel.FirstRunDocumentStatusText));
            Assert.Contains("Diagnostics", viewModel.FirstRunRecoveryText, StringComparison.Ordinal);

            settings.SetBool(Keys.UpdateCheckEnabled, true);

            Assert.Contains("Automatic update checks are enabled", viewModel.FirstRunPrivacyText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RefreshCommand_WhenCurrentFileWasRemovedExternally_LoadsNextAvailableImage()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "a.png");
            var second = WritePng(temp.Path, "b.png");
            using var viewModel = CreateViewModelWithFastPreview(temp);

            viewModel.OpenFile(first);
            File.Delete(first);

            viewModel.RefreshCommand.Execute(null);

            Assert.Equal(second, viewModel.CurrentPath);
            Assert.True(viewModel.HasImage);
            Assert.Equal("b.png", viewModel.CurrentFileName);
            Assert.Equal("1 / 1", viewModel.PositionText);
            Assert.Empty(viewModel.FolderPreviewItems);
            Assert.False(viewModel.ShowFilmstrip);
            Assert.Equal("Folder refreshed", viewModel.ToastMessage);
        });
    }

    [Fact]
    public void MetadataControllerState_RelaysThroughMainViewModel()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var path = WritePng(temp.Path, "photo.png");
            var changed = new List<string>();
            using var metadata = new PhotoMetadataController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                currentPath: () => path,
                readMetadata: _ => new PhotoMetadata([new MetadataFact("Camera", "RelayCam")]),
                timeout: TimeSpan.FromSeconds(1));
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: null,
                photoMetadata: metadata,
                ocrWorkflow: null,
                externalEditReload: null,
                updateCheck: null);

            viewModel.PropertyChanged += RecordChangedProperty(changed);

            metadata.Refresh(path);
            PumpUntil(() => !viewModel.IsMetadataLoading && viewModel.PhotoMetadataRows.Count == 1);

            var row = Assert.Single(viewModel.PhotoMetadataRows);
            Assert.Equal("Camera", row.Label);
            Assert.Equal("RelayCam", row.Value);
            Assert.Equal("", viewModel.MetadataStatusText);
            Assert.Contains(nameof(MainViewModel.IsMetadataLoading), changed);
            Assert.Contains(nameof(MainViewModel.MetadataStatusText), changed);
        });
    }

    [Fact]
    public void OcrWorkflowState_RelaysStatusPropertiesThroughMainViewModel()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var changed = new List<string>();
            using var ocr = new OcrWorkflowController(
                currentPath: () => @"C:\photos\text.png",
                hasImage: () => true,
                notify: _ => { },
                extractLinesAsync: (_, _) => Task.FromResult<IReadOnlyList<OcrTextLine>?>([]));
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: null,
                photoMetadata: null,
                ocrWorkflow: ocr,
                externalEditReload: null,
                updateCheck: null);

            viewModel.PropertyChanged += RecordChangedProperty(changed);

            viewModel.OcrOverlayLines = new ObservableCollection<OcrTextLine>
            {
                new()
                {
                    Text = "Invoice",
                    BoundingBox = new Windows.Foundation.Rect(4, 8, 80, 24)
                }
            };
            viewModel.IsOcrMode = true;

            Assert.True(viewModel.IsOcrMode);
            Assert.True(viewModel.ShowOcrStatusPanel);
            Assert.Equal("Text overlay active", viewModel.OcrStatusTitle);
            Assert.Equal("1 text region found", viewModel.OcrRegionCountText);
            Assert.Equal("1 text region found. Select a text box and press Ctrl+C to copy.", viewModel.OcrStatusDetail);
            Assert.Equal("Hide text overlay (E)", viewModel.OcrModeTooltip);
            Assert.Contains(nameof(MainViewModel.OcrOverlayLines), changed);
            Assert.Contains(nameof(MainViewModel.OcrRegionCountText), changed);
            Assert.Contains(nameof(MainViewModel.ShowOcrStatusPanel), changed);
            Assert.Contains(nameof(MainViewModel.OcrModeTooltip), changed);
        });
    }

    [Fact]
    public void UpdateCheckState_RelaysAvailableUpdateThroughMainViewModel()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var recorded = new List<UpdateCheckService.CheckResult>();
            var update = new UpdateCheckController(
                notify: _ => { },
                checkAsync: _ => Task.FromResult(new UpdateCheckService.CheckResult(
                    NewerAvailable: true,
                    LatestTag: "v9.9.9",
                    LatestHtmlUrl: "https://example.test/releases/v9.9.9",
                    Error: null,
                    ShouldUpdateLastChecked: true)),
                recordLastChecked: recorded.Add);
            var changed = new List<string>();
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: null,
                photoMetadata: null,
                ocrWorkflow: null,
                externalEditReload: null,
                updateCheck: update);

            viewModel.PropertyChanged += RecordChangedProperty(changed);

            viewModel.CheckForUpdatesAsync(userInitiated: true).GetAwaiter().GetResult();

            Assert.Equal("v9.9.9", viewModel.LatestUpdateTag);
            Assert.Equal("https://example.test/releases/v9.9.9", viewModel.LatestUpdateUrl);
            Assert.True(viewModel.HasUpdateAvailable);
            Assert.True(viewModel.OpenLatestUpdateCommand.CanExecute(null));
            Assert.Single(recorded);
            Assert.Contains(nameof(MainViewModel.LatestUpdateTag), changed);
            Assert.Contains(nameof(MainViewModel.HasUpdateAvailable), changed);
        });
    }

    [Fact]
    public void UpdateCheckState_RelaysBusyStatusThroughMainViewModel()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var gate = new TaskCompletionSource<UpdateCheckService.CheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var update = new UpdateCheckController(
                notify: _ => { },
                checkAsync: _ => gate.Task);
            var changed = new List<string>();
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: null,
                photoMetadata: null,
                ocrWorkflow: null,
                externalEditReload: null,
                updateCheck: update);

            viewModel.PropertyChanged += RecordChangedProperty(changed);

            var running = viewModel.CheckForUpdatesAsync(userInitiated: true);

            Assert.True(viewModel.IsCheckingForUpdates);
            Assert.Equal("Checking GitHub Releases...", viewModel.UpdateCheckStatusText);
            Assert.False(viewModel.CheckForUpdatesCommand.CanExecute(null));
            Assert.Contains(nameof(MainViewModel.IsCheckingForUpdates), changed);
            Assert.Contains(nameof(MainViewModel.UpdateCheckStatusText), changed);

            gate.SetResult(new UpdateCheckService.CheckResult(
                NewerAvailable: false,
                LatestTag: "v0.2.9",
                LatestHtmlUrl: null,
                Error: null,
                ShouldUpdateLastChecked: true));
            running.GetAwaiter().GetResult();

            Assert.False(viewModel.IsCheckingForUpdates);
            Assert.Equal("", viewModel.UpdateCheckStatusText);
            Assert.True(viewModel.CheckForUpdatesCommand.CanExecute(null));
        });
    }

    [Fact]
    public void UpdateCheckState_WhenOffline_ShowsSecondaryRecoveryStatus()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var update = new UpdateCheckController(
                notify: _ => { },
                checkAsync: _ => Task.FromResult(new UpdateCheckService.CheckResult(
                    NewerAvailable: false,
                    LatestTag: null,
                    LatestHtmlUrl: null,
                    Error: "network: offline",
                    ShouldUpdateLastChecked: false)));
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: null,
                photoMetadata: null,
                ocrWorkflow: null,
                externalEditReload: null,
                updateCheck: update);

            viewModel.CheckForUpdatesAsync(userInitiated: true).GetAwaiter().GetResult();

            Assert.True(viewModel.HasUpdateCheckIssue);
            Assert.Equal("Update check unavailable", viewModel.UpdateCheckIssueTitle);
            Assert.True(viewModel.HasSecondaryStatus);
            Assert.Equal("Update check unavailable", viewModel.SecondaryStatusTitle);
            Assert.Contains("no image files were uploaded", viewModel.SecondaryStatusDetail);
            Assert.Equal(MainViewModel.SecondaryStatusToneKind.Warning, viewModel.SecondaryStatusTone);
        });
    }

    [Fact]
    public void FolderPreviewState_WhenThumbnailLoadFails_ShowsSecondaryRecoveryStatus()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "a.png");
            WritePng(temp.Path, "b.png");
            var folderPreview = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) => throw new IOException("cache unavailable"));
            using var viewModel = new MainViewModel(
                CreateSettings(temp),
                clipboardImport: null,
                navigator: null,
                recycleBinDelete: null,
                folderPreview: folderPreview,
                photoMetadata: null,
                ocrWorkflow: null,
                externalEditReload: null,
                updateCheck: null);

            viewModel.OpenFile(first);
            PumpUntil(() => viewModel.ThumbnailFailureCount == 2);

            Assert.True(viewModel.HasThumbnailFailures);
            Assert.Equal(2, viewModel.ThumbnailFailureCount);
            Assert.True(viewModel.HasSecondaryStatus);
            Assert.Equal("Some thumbnails could not be shown", viewModel.SecondaryStatusTitle);
            Assert.Contains("Refresh the folder", viewModel.SecondaryStatusDetail);
            Assert.Equal(MainViewModel.SecondaryStatusToneKind.Warning, viewModel.SecondaryStatusTone);
        });
    }

    private static MainViewModel CreateViewModel(TestDirectory temp, ClipboardImportService? clipboardImport = null)
        => new(CreateSettings(temp), clipboardImport);

    private static MainViewModel CreateViewModelWithFastPreview(TestDirectory temp, ClipboardImportService? clipboardImport = null)
        => new(
            CreateSettings(temp),
            clipboardImport,
            navigator: null,
            recycleBinDelete: null,
            folderPreview: new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) => null),
            photoMetadata: null,
            ocrWorkflow: null,
            externalEditReload: null,
            updateCheck: null);

    private static SettingsService CreateSettings(TestDirectory temp)
        => new(Path.Combine(temp.Path, "settings.db"));

    private static string WritePng(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var bitmap = CreateBitmap();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static string WriteTwoPageTiff(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var encoder = new TiffBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap()));
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap()));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static string WriteTwoPageCbz(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WritePngArchiveEntry(archive, "page1.png");
        WritePngArchiveEntry(archive, "page2.png");
        return path;
    }

    private static void WritePngArchiveEntry(ZipArchive archive, string name)
    {
        using var encoded = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateBitmap()));
        encoder.Save(encoded);
        encoded.Position = 0;

        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        encoded.CopyTo(stream);
    }

    private static BitmapSource CreateBitmap()
    {
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[]
            {
                0x28, 0xA7, 0x45, 0xFF, 0x28, 0xA7, 0x45, 0xFF,
                0x28, 0xA7, 0x45, 0xFF, 0x28, 0xA7, 0x45, 0xFF
            },
            8);
        bitmap.Freeze();
        return bitmap;
    }

    private static PropertyChangedEventHandler RecordChangedProperty(ICollection<string> changed)
        => (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
                changed.Add(e.PropertyName);
        };

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Timed out while waiting for dispatcher work.");

            PumpFor(TimeSpan.FromMilliseconds(10));
        }
    }

    private static void PumpFor(TimeSpan interval)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class FakeClipboardDataSource : IClipboardDataSource
    {
        public BitmapSource? Image { get; init; }
        public IReadOnlyList<string> Files { get; init; } = [];

        public bool ContainsFileDropList() => Files.Count > 0;

        public IReadOnlyList<string> GetFileDropList() => Files;

        public bool ContainsImage() => Image is not null;

        public BitmapSource? GetImage() => Image;
    }
}
