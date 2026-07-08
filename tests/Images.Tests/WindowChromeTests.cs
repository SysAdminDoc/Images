using Images.Services;
using System.Reflection;

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

    [Fact]
    public void LatteCaptionColor_UsesLatteBase()
    {
        var field = typeof(WindowChrome).GetField("LatteBaseColorRef", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(0x00F5F1EF, Assert.IsType<int>(field.GetRawConstantValue()));
    }
}
