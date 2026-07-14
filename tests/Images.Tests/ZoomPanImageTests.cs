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

    [Fact]
    public void ApplyZoomToViewportRect_CenteredBox_ScalesAndStaysCentered()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { Source = MakeBitmap() };
            Arrange(control, 400, 300);
            control.SetViewState(new ZoomPanViewState(1, 0, 0));

            // Centered 200x150 box in a 400x300 viewport -> 2x fill, still centered.
            control.ApplyZoomToViewportRect(new Rect(100, 75, 200, 150));

            var state = control.GetViewState();
            Assert.Equal(2.0, state.Scale, 3);
            Assert.Equal(0.0, state.TranslateX, 3);
            Assert.Equal(0.0, state.TranslateY, 3);
        });
    }

    [Fact]
    public void ApplyZoomToViewportRect_IgnoresClickSizedBox()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { Source = MakeBitmap() };
            Arrange(control, 400, 300);
            var before = new ZoomPanViewState(1.5, 10, -5);
            control.SetViewState(before);

            control.ApplyZoomToViewportRect(new Rect(50, 50, 3, 3));

            Assert.Equal(before, control.GetViewState());
        });
    }

    [Fact]
    public void ComputeLoupeViewbox_CenteredPoint_ReturnsCenteredRegion()
    {
        var box = ZoomPanImage.ComputeLoupeViewbox(50, 50, 100, 100, 20, 20);

        Assert.Equal(0.40, box.X, 3);
        Assert.Equal(0.40, box.Y, 3);
        Assert.Equal(0.20, box.Width, 3);
        Assert.Equal(0.20, box.Height, 3);
    }

    [Fact]
    public void ComputeLoupeViewbox_NearEdge_ClampsInsideBounds()
    {
        var box = ZoomPanImage.ComputeLoupeViewbox(0, 0, 100, 100, 20, 20);

        Assert.Equal(0.0, box.X, 3);
        Assert.Equal(0.0, box.Y, 3);
        Assert.True(box.X + box.Width <= 1.0 + 1e-9);
        Assert.True(box.Y + box.Height <= 1.0 + 1e-9);
    }

    [Fact]
    public void MiddleButton_TogglesLoupeActiveState()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { Source = MakeBitmap() };
            Arrange(control, 400, 300);

            control.RaiseEvent(MiddleButtonEvent(control, System.Windows.Input.Mouse.MouseDownEvent));
            Assert.True(control.IsLoupeActive);

            control.RaiseEvent(MiddleButtonEvent(control, System.Windows.Input.Mouse.MouseUpEvent));
            Assert.False(control.IsLoupeActive);
        });
    }

    [Fact]
    public void MiddleButton_DoesNotActivateLoupeForOnePixelPlaceholderSource()
    {
        RunOnSta(() =>
        {
            // Tile-backed (gigapixel) images expose a 1x1 placeholder; the loupe must not engage.
            var placeholder = BitmapSource.Create(
                1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0xFF }, 4);
            placeholder.Freeze();
            var control = new ZoomPanImage { Source = placeholder };
            Arrange(control, 400, 300);

            control.RaiseEvent(MiddleButtonEvent(control, System.Windows.Input.Mouse.MouseDownEvent));

            Assert.False(control.IsLoupeActive);
        });
    }

    [Fact]
    public void ToggleKeyboardLoupe_TurnsCenteredLoupeOnAndOff()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { Source = MakeBitmap() };
            Arrange(control, 400, 300);

            Assert.True(control.ToggleKeyboardLoupe());
            Assert.True(control.IsKeyboardLoupeActive);

            Assert.False(control.ToggleKeyboardLoupe());
            Assert.False(control.IsKeyboardLoupeActive);
        });
    }

    [Fact]
    public void ToggleKeyboardLoupe_IgnoresOnePixelPlaceholderSource()
    {
        RunOnSta(() =>
        {
            var placeholder = BitmapSource.Create(
                1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0xFF }, 4);
            placeholder.Freeze();
            var control = new ZoomPanImage { Source = placeholder };
            Arrange(control, 400, 300);

            Assert.False(control.ToggleKeyboardLoupe());
            Assert.False(control.IsKeyboardLoupeActive);
        });
    }

    [Fact]
    public void KeyboardLoupe_SurvivesMiddleButtonPointerLoupeRelease()
    {
        RunOnSta(() =>
        {
            var control = new ZoomPanImage { Source = MakeBitmap() };
            Arrange(control, 400, 300);
            Assert.True(control.ToggleKeyboardLoupe());

            // A held middle-button loupe overrides position, then releasing reverts to the
            // still-active centered keyboard loupe rather than hiding it.
            control.RaiseEvent(MiddleButtonEvent(control, System.Windows.Input.Mouse.MouseDownEvent));
            Assert.True(control.IsLoupeActive);
            control.RaiseEvent(MiddleButtonEvent(control, System.Windows.Input.Mouse.MouseUpEvent));

            Assert.False(control.IsLoupeActive);
            Assert.True(control.IsKeyboardLoupeActive);
        });
    }

    private static System.Windows.Input.MouseButtonEventArgs MiddleButtonEvent(ZoomPanImage control, RoutedEvent routed)
        => new(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Middle)
        {
            RoutedEvent = routed,
            Source = control,
        };

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
