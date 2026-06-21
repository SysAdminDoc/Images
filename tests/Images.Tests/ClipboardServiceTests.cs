using Images.Services;

namespace Images.Tests;

public sealed class ClipboardServiceTests
{
    [Fact]
    public void SetText_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ClipboardService.SetText(null!));
    }
}
