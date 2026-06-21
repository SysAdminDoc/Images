using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class CrashLogTests
{
    [Fact]
    public void LogPath_IsUnderLocalAppDataImages()
    {
        var logPath = CrashLog.LogPath;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, logPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Images", logPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogPath_EndsWith_CrashLog()
    {
        var logPath = CrashLog.LogPath;

        Assert.EndsWith("crash.log", logPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_WritesExceptionToLogFile()
    {
        var testException = new InvalidOperationException("Test crash log entry");

        CrashLog.Append("UnitTest", testException, "test context");

        var content = File.ReadAllText(CrashLog.LogPath);
        Assert.Contains("UnitTest", content);
        Assert.Contains("Test crash log entry", content);
        Assert.Contains("test context", content);
    }

    [Fact]
    public void Append_WithNullException_DoesNotThrow()
    {
        var ex = Record.Exception(() => CrashLog.Append("UnitTest", null));

        Assert.Null(ex);
    }
}
