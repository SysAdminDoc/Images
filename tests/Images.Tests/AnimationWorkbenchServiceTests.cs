using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class AnimationWorkbenchServiceTests
{
    [Fact]
    public void ClampPlaybackSpeed_RejectsInvalidValues()
    {
        Assert.Equal(1.0, AnimationWorkbenchService.ClampPlaybackSpeed(double.NaN));
        Assert.Equal(0.25, AnimationWorkbenchService.ClampPlaybackSpeed(0.01));
        Assert.Equal(4.0, AnimationWorkbenchService.ClampPlaybackSpeed(9));
        Assert.Equal(2.5, AnimationWorkbenchService.ClampPlaybackSpeed(2.5));
    }

    [Fact]
    public void DelayForSpeed_ClampsToUiTimerFloor()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(50), AnimationWorkbenchService.DelayForSpeed(TimeSpan.FromMilliseconds(100), 2));
        Assert.Equal(TimeSpan.FromMilliseconds(16), AnimationWorkbenchService.DelayForSpeed(TimeSpan.FromMilliseconds(1), 4));
    }

    [Fact]
    public void FormatTimestamp_UsesElapsedDelayBeforeSelectedFrame()
    {
        var delays = new[]
        {
            TimeSpan.FromMilliseconds(80),
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(300)
        };

        Assert.Equal("00:00.000", AnimationWorkbenchService.FormatTimestamp(delays, 0));
        Assert.Equal("00:00.080", AnimationWorkbenchService.FormatTimestamp(delays, 1));
        Assert.Equal("00:00.200", AnimationWorkbenchService.FormatTimestamp(delays, 2));
    }

    [Fact]
    public void CreateDefaultFrameExportFileName_SanitizesSourceName()
    {
        var name = AnimationWorkbenchService.CreateDefaultFrameExportFileName(@"C:\images\bad:name.gif", 4);

        Assert.Equal("bad-name-frame-005.png", name);
    }

    [Fact]
    public void SaveFramePng_WritesReadablePng()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "frame.png");

        AnimationWorkbenchService.SaveFramePng(CreateBitmap(), path);

        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoded = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(2, decoded.PixelWidth);
        Assert.Equal(2, decoded.PixelHeight);
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
                0x10, 0x20, 0x30, 0xFF, 0x10, 0x20, 0x30, 0xFF,
                0x10, 0x20, 0x30, 0xFF, 0x10, 0x20, 0x30, 0xFF
            },
            8);
        bitmap.Freeze();
        return bitmap;
    }
}
