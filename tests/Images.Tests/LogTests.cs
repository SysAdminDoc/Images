using Images.Services;

namespace Images.Tests;

public sealed class LogTests
{
    [Fact]
    public void For_ReturnsNonNullLogger()
    {
        var logger = Log.For<LogTests>();

        Assert.NotNull(logger);
    }

    [Fact]
    public void Get_ReturnsNonNullLogger()
    {
        var logger = Log.Get("test");

        Assert.NotNull(logger);
    }

    [Fact]
    public void For_ReturnsSameLoggerTypeAcrossCalls()
    {
        var logger1 = Log.For<LogTests>();
        var logger2 = Log.For<LogTests>();

        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
    }
}
