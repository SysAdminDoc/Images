using System.Threading;
using System.Windows;
using Images.Controls;

namespace Images.Tests;

public sealed class ZoomPanImageTests
{
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
