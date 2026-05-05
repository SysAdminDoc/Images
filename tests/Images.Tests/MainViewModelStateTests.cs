using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            using var viewModel = CreateViewModel(temp);

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
            Assert.Equal("Pasted from clipboard", viewModel.ToastMessage);
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
            using var viewModel = CreateViewModel(temp);

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

    private static MainViewModel CreateViewModel(TestDirectory temp, ClipboardImportService? clipboardImport = null)
        => new(CreateSettings(temp), clipboardImport);

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

        public bool ContainsFileDropList() => false;

        public IReadOnlyList<string> GetFileDropList() => [];

        public bool ContainsImage() => Image is not null;

        public BitmapSource? GetImage() => Image;
    }
}
