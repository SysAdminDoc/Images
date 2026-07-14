using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.ViewModels;

namespace Images.Tests;

public sealed class ArchiveContinuousPageItemTests
{
    [Fact]
    public async Task EnsureLoadedAsync_IsLazyAndPublishesDecodedPage()
    {
        var calls = 0;
        var bitmap = CreateBitmap(10, 20);
        using var item = new ArchiveContinuousPageItem(
            pageIndex: 1,
            pageCount: 3,
            (index, _) =>
            {
                calls++;
                Assert.Equal(1, index);
                return Task.FromResult(bitmap);
            });

        Assert.Equal(0, calls);
        Assert.False(item.HasImage);

        await item.EnsureLoadedAsync();

        Assert.Equal(1, calls);
        Assert.Same(bitmap, item.Image);
        Assert.Equal("Page 2 of 3", item.PageLabel);
        Assert.False(item.IsLoading);
        Assert.False(item.HasError);
    }

    [Fact]
    public async Task Release_CancelsRealizedLoadWithoutShowingAnError()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var item = new ArchiveContinuousPageItem(
            pageIndex: 0,
            pageCount: 2,
            async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateBitmap(1, 1);
            });

        var load = item.EnsureLoadedAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        item.Release();
        await load.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Null(item.Image);
        Assert.False(item.IsLoading);
        Assert.False(item.HasError);
    }

    [Fact]
    public async Task RetryAsync_RecoversAfterSanitizedPageError()
    {
        var calls = 0;
        var bitmap = CreateBitmap(2, 2);
        using var item = new ArchiveContinuousPageItem(
            pageIndex: 0,
            pageCount: 1,
            (_, _) => ++calls == 1
                ? Task.FromException<BitmapSource>(new IOException("sensitive decoder detail"))
                : Task.FromResult(bitmap));

        await item.EnsureLoadedAsync();

        Assert.True(item.HasError);
        Assert.DoesNotContain("sensitive", item.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await item.RetryAsync();

        Assert.Equal(2, calls);
        Assert.Same(bitmap, item.Image);
        Assert.False(item.HasError);
    }

    private static BitmapSource CreateBitmap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
