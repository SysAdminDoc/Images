using Images.Services;

namespace Images.Tests;

public sealed class ImageEventSourceTests
{
    [Fact]
    public void Instance_IsNotNull()
    {
        Assert.NotNull(ImageEventSource.Instance);
    }

    [Fact]
    public void RecordDecodeAttempt_DoesNotThrow()
    {
        var ex = Record.Exception(() => ImageEventSource.Instance.RecordDecodeAttempt());

        Assert.Null(ex);
    }

    [Fact]
    public void RecordWicDecode_DoesNotThrow()
    {
        var ex = Record.Exception(() => ImageEventSource.Instance.RecordWicDecode());

        Assert.Null(ex);
    }

    [Fact]
    public void RecordMagickFallbackDecode_DoesNotThrow()
    {
        var ex = Record.Exception(() => ImageEventSource.Instance.RecordMagickFallbackDecode());

        Assert.Null(ex);
    }
}
