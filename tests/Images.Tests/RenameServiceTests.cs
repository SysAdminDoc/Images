using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class RenameServiceTests
{
    [Fact]
    public void Sanitize_CollapsesWhitespaceAndTrimsUnsafeSuffix()
    {
        var sanitized = RenameService.Sanitize("  family\t\tphoto  .  ");

        Assert.Equal("family photo", sanitized);
    }

    [Fact]
    public void Sanitize_WhenInputHasOnlyInvalidOrWhitespaceCharacters_ReturnsEmpty()
    {
        var sanitized = RenameService.Sanitize("  :::   ...  ");

        Assert.Equal(string.Empty, sanitized);
    }

    [Fact]
    public void Commit_WhenStemHasNoValidCharacters_ThrowsAndLeavesSource()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg");
        var service = new RenameService();

        var ex = Assert.Throws<ArgumentException>(() => service.Commit(source, ":::", ".jpg"));

        Assert.Equal("desiredStem", ex.ParamName);
        Assert.True(File.Exists(source));
        Assert.Empty(service.UndoHistory);
    }

    [Fact]
    public void Commit_WhenExtensionIsUnsupported_ThrowsBeforeMovingSource()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var unsupportedTarget = Path.Combine(temp.Path, "photo.txt");
        var service = new RenameService();

        var ex = Assert.Throws<ArgumentException>(() => service.Commit(source, "photo", ".txt"));

        Assert.Equal("extension", ex.ParamName);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(unsupportedTarget));
        Assert.Empty(service.UndoHistory);
    }

    [Fact]
    public void Commit_WhenImageIsRenamedToArchiveExtension_ThrowsBeforeMovingSource()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var service = new RenameService();

        var ex = Assert.Throws<ArgumentException>(() => service.Commit(source, "photo", ".cbz"));

        Assert.Equal("extension", ex.ParamName);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(temp.Path, "photo.cbz")));
    }

    [Fact]
    public void Commit_WhenArchiveStaysArchiveExtension_AllowsRename()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("book.cbz", "source");
        var service = new RenameService();

        var result = service.Commit(source, "renamed", ".zip");

        Assert.Equal(Path.Combine(temp.Path, "renamed.zip"), result);
        Assert.False(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(result));
    }

    [Fact]
    public void Commit_WhenTargetExists_UsesDeterministicConflictSuffix()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        temp.WriteFile("renamed.jpg", "existing");
        var expected = Path.Combine(temp.Path, "renamed (2).jpg");
        var service = new RenameService();

        var result = service.Commit(source, "renamed", ".jpg");

        Assert.Equal(expected, result);
        Assert.False(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(expected));
        Assert.Single(service.UndoHistory);
    }

    [Fact]
    public void Revert_WhenOriginalPathIsOccupied_UsesConflictSuffix()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var service = new RenameService();
        var renamed = service.Commit(source, "renamed", ".jpg");
        var entry = Assert.Single(service.UndoHistory);
        temp.WriteFile("photo.jpg", "new occupant");
        var expected = Path.Combine(temp.Path, "photo (2).jpg");

        var result = service.Revert(entry);

        Assert.NotNull(result);
        Assert.Equal(expected, result!.ToPath);
        Assert.Equal("source", File.ReadAllText(expected));
        Assert.Equal("new occupant", File.ReadAllText(source));
        Assert.False(File.Exists(renamed));
    }
}
