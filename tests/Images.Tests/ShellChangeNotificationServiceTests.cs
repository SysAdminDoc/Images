using Images.Services;

namespace Images.Tests;

public sealed class ShellChangeNotificationServiceTests
{
    [Fact]
    public void NotifyFileUpdated_WithExistingFile_DoesNotThrow()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("test.jpg", "dummy");

        var ex = Record.Exception(() => ShellChangeNotificationService.NotifyFileUpdated(path));

        Assert.Null(ex);
    }

    [Fact]
    public void NotifyFileUpdated_WithNonexistentFile_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            ShellChangeNotificationService.NotifyFileUpdated(@"C:\__nonexistent_test__\image.jpg"));

        Assert.Null(ex);
    }
}
