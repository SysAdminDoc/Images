using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Controls;

namespace Images.Tests;

public sealed class ZoomPanImageTests
{
    [Fact]
    public void PreserveViewOnSourceChange_KeepsZoomAndCentersPanForNewImage()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { PreserveViewOnSourceChange = true };
            Arrange(control, 400, 300);
            control.SetViewState(new ZoomPanViewState(3, 48, -32));

            control.Source = MakeBitmap();

            Assert.Equal(new ZoomPanViewState(3, 0, 0), control.GetViewState());
        });
    }

    [Fact]
    public void SourceChange_WithoutPreserve_ResetsView()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage();
            Arrange(control, 400, 300);
            control.SetViewState(new ZoomPanViewState(3, 48, -32));

            control.Source = MakeBitmap();

            Assert.Equal(new ZoomPanViewState(1, 0, 0), control.GetViewState());
        });
    }

    [Theory]
    [InlineData("Bgra32", true)]
    [InlineData("Pbgra32", true)]
    [InlineData("Bgr32", false)]
    [InlineData("Bgr24", false)]
    public void HasAlphaChannel_DetectsAlphaCarryingFormats(string format, bool expected)
    {
        var pixelFormat = format switch
        {
            "Bgra32" => PixelFormats.Bgra32,
            "Pbgra32" => PixelFormats.Pbgra32,
            "Bgr32" => PixelFormats.Bgr32,
            _ => PixelFormats.Bgr24,
        };

        Assert.Equal(expected, ZoomPanImage.HasAlphaChannel(pixelFormat));
    }

    private static BitmapSource MakeBitmap()
    {
        var bitmap = BitmapSource.Create(
            2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0x80,
                0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00
            },
            8);
        bitmap.Freeze();
        return bitmap;
    }

    [Fact]
    public void SizeChanged_PreservesUserModifiedViewState()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage();
            Arrange(control, 400, 300);

            control.SetViewState(new ZoomPanViewState(2.25, 48, -32));

            Arrange(control, 520, 360);

            Assert.Equal(new ZoomPanViewState(2.25, 48, -32), control.GetViewState());
        });
    }

    [Fact]
    public void SizeChanged_KeepsUntouchedFitStateAtFit()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage();
            Arrange(control, 400, 300);

            Arrange(control, 520, 360);

            Assert.Equal(new ZoomPanViewState(1, 0, 0), control.GetViewState());
        });
    }

    [Fact]
    public void RightDoubleClick_DoesNotResetViewState()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage();
            Arrange(control, 400, 300);
            var expected = new ZoomPanViewState(2, 24, -16);
            control.SetViewState(expected);

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = Control.MouseDoubleClickEvent,
                Source = control
            };
            control.RaiseEvent(args);

            Assert.False(args.Handled);
            Assert.Equal(expected, control.GetViewState());
        });
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        var size = new Size(width, height);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
