using System.IO;

namespace Images.Tests;

public sealed class MainWindowDropTests
{
    [Fact]
    public void ResolveDropOpenRequest_UsesFileListForMultipleSupportedFiles()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.png", "not decoded here");
        var second = temp.WriteFile("b.jpg", "not decoded here");
        var unsupported = temp.WriteFile("notes.txt", "ignored");

        var request = MainWindow.ResolveDropOpenRequest([first, unsupported, second]);

        Assert.Equal(MainWindow.DropOpenKind.FileList, request.Kind);
        Assert.Equal([first, second], request.Paths);
    }

    [Fact]
    public void ResolveDropOpenRequest_UsesLaterSupportedFileWhenFirstFileUnsupported()
    {
        using var temp = TestDirectory.Create();
        var unsupported = temp.WriteFile("notes.txt", "ignored");
        var image = temp.WriteFile("photo.webp", "not decoded here");

        var request = MainWindow.ResolveDropOpenRequest([unsupported, image]);

        Assert.Equal(MainWindow.DropOpenKind.SingleFile, request.Kind);
        Assert.Equal([image], request.Paths);
    }

    [Fact]
    public void ResolveDropOpenRequest_PreservesDirectoryDrop()
    {
        using var temp = TestDirectory.Create();
        var folder = Path.Combine(temp.Path, "album");
        Directory.CreateDirectory(folder);
        var image = temp.WriteFile("photo.png", "not decoded here");

        var request = MainWindow.ResolveDropOpenRequest([folder, image]);

        Assert.Equal(MainWindow.DropOpenKind.Folder, request.Kind);
        Assert.Equal([folder], request.Paths);
    }
}
