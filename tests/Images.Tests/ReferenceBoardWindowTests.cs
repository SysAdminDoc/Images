using System.Threading;
using System.Windows.Controls;

namespace Images.Tests;

public sealed class ReferenceBoardWindowTests
{
    [Fact]
    public void DragMouseUp_HandlesOnlyActiveDragHandle()
    {
        RunOnSta(() =>
        {
            var element = new Border();
            var activeHandle = new Border();
            var otherHandle = new Border();

            Assert.True(ReferenceBoardWindow.ShouldHandleDragMouseUp(element, activeHandle, activeHandle));
            Assert.False(ReferenceBoardWindow.ShouldHandleDragMouseUp(null, activeHandle, activeHandle));
            Assert.False(ReferenceBoardWindow.ShouldHandleDragMouseUp(element, null, activeHandle));
            Assert.False(ReferenceBoardWindow.ShouldHandleDragMouseUp(element, activeHandle, otherHandle));
        });
    }

    [Fact]
    public void LostMouseCapture_ClearsOnlyActiveDragHandle()
    {
        RunOnSta(() =>
        {
            var activeHandle = new Border();
            var otherHandle = new Border();

            Assert.True(ReferenceBoardWindow.ShouldClearDragForLostCapture(activeHandle, activeHandle));
            Assert.False(ReferenceBoardWindow.ShouldClearDragForLostCapture(null, activeHandle));
            Assert.False(ReferenceBoardWindow.ShouldClearDragForLostCapture(activeHandle, otherHandle));
        });
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
