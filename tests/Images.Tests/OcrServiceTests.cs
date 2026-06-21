using Images.Services;

namespace Images.Tests;

public sealed class OcrServiceTests
{
    [Fact]
    public void CanConstruct()
    {
        var service = new OcrService();
        Assert.NotNull(service);
    }
}
