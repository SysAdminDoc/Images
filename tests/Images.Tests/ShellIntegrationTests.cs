using Images.Services;

namespace Images.Tests;

public sealed class ShellIntegrationTests
{
    [Fact]
    public void OpenShellTarget_WithNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShellIntegration.OpenShellTarget(null!));
    }

    [Fact]
    public void OpenShellTarget_WithEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShellIntegration.OpenShellTarget(""));
    }

    [Fact]
    public void OpenShellTarget_WithWhitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShellIntegration.OpenShellTarget("   "));
    }

    [Fact]
    public void OpenFolder_WithNull_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShellIntegration.OpenFolder(null!));
    }

    [Fact]
    public void RevealPathInExplorer_WithEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShellIntegration.RevealPathInExplorer(""));
    }
}
