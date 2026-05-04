using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class DirectoryNavigatorTests
{
    [Fact]
    public void Refresh_WhenCurrentFileDisappears_ClampsToValidItem()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        var second = temp.WriteFile("b.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(second));
        Assert.Equal(second, nav.CurrentPath);

        File.Delete(second);
        nav.Refresh();

        Assert.Equal(1, nav.Count);
        Assert.Equal(0, nav.CurrentIndex);
        Assert.Equal(first, nav.CurrentPath);
    }

    [Fact]
    public void UpdateCurrentPath_WhenTargetDisappears_ThrowsAndKeepsCurrentItem()
    {
        using var temp = TestDirectory.Create();
        var current = temp.WriteFile("photo.jpg");
        var missing = Path.Combine(temp.Path, "renamed.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(current));

        var ex = Assert.Throws<FileNotFoundException>(() => nav.UpdateCurrentPath(missing));

        Assert.Equal(missing, ex.FileName);
        Assert.Equal(current, nav.CurrentPath);
    }
}
