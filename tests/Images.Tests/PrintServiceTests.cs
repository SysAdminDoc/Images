using Images.Services;

namespace Images.Tests;

public sealed class PrintServiceTests
{
    [Fact]
    public void ServiceClassExists()
    {
        Assert.NotNull(typeof(PrintService));
        Assert.True(typeof(PrintService).IsAbstract && typeof(PrintService).IsSealed);
    }
}
