using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class MainViewModelInpaintTests
{
    [Fact]
    public void OpenFile_ResetsLatentInpaintState()
    {
        RunOnSta(() =>
        {
            using var temp = TestDirectory.Create();
            var first = WritePng(temp.Path, "first.png");
            var second = WritePng(temp.Path, "second.png");
            using var viewModel = CreateViewModel(temp);

            viewModel.OpenFile(first);
            viewModel.IsInpaintMode = true;
            viewModel.AddInpaintMaskRegion(10, 10);

            Assert.True(viewModel.IsInpaintMode);
            Assert.True(viewModel.HasInpaintMaskRegions);

            viewModel.OpenFile(second);

            Assert.Equal(second, viewModel.CurrentPath);
            Assert.False(viewModel.IsInpaintMode);
            Assert.False(viewModel.HasInpaintMaskRegions);
            Assert.Empty(viewModel.InpaintMaskRegions);
        });
    }

    private static MainViewModel CreateViewModel(TestDirectory temp)
        => new(
            new SettingsService(Path.Combine(temp.Path, "settings.db")),
            clipboardImport: null,
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

    private static string WritePng(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
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
            throw exception;
    }
}
