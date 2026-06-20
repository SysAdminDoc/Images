using Images.Services;

namespace Images.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void Current_DisplayVersion_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Current.DisplayVersion));
    }

    [Fact]
    public void Current_RuntimeDescription_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Current.RuntimeDescription));
    }

    [Fact]
    public void Current_OsDescription_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Current.OsDescription));
    }

    [Fact]
    public void Current_BinaryPath_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Current.BinaryPath));
    }

    [Fact]
    public void AppInfoSnapshot_RecordEquality()
    {
        var a = new AppInfoSnapshot("1.0", "1.0+abc", "1.0.0.0", ".NET 10", "Win11", @"C:\app.exe");
        var b = new AppInfoSnapshot("1.0", "1.0+abc", "1.0.0.0", ".NET 10", "Win11", @"C:\app.exe");

        Assert.Equal(a, b);
    }
}
