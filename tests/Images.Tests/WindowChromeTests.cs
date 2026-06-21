using Images.Services;

namespace Images.Tests;

public sealed class WindowChromeTests
{
    [Fact]
    public void ApplyDarkCaption_WithZeroHandle_DoesNotThrow()
    {
        var ex = Record.Exception(() => WindowChrome.ApplyDarkCaption(IntPtr.Zero));

        Assert.Null(ex);
    }

    [Fact]
    public void ApplyDarkCaption_WithZeroHandle_ReturnsImmediately()
    {
        // The method is documented to early-return on IntPtr.Zero.
        // Calling twice should also be safe.
        var ex = Record.Exception(() =>
        {
            WindowChrome.ApplyDarkCaption(IntPtr.Zero);
            WindowChrome.ApplyDarkCaption(IntPtr.Zero);
        });

        Assert.Null(ex);
    }
}
