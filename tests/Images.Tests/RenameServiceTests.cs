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
    public void Commit_MovesBothSidecarFormsAndRevertMovesThemBack()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        File.WriteAllText(source + ".xmp", "full-name sidecar");
        File.WriteAllText(Path.Combine(temp.Path, "photo.xmp"), "stem sidecar");
        var service = new RenameService();

        var renamed = service.Commit(source, "renamed", ".jpg");
        var entry = Assert.Single(service.UndoHistory);

        var renamedFullSidecar = renamed + ".xmp";
        var renamedStemSidecar = Path.Combine(temp.Path, "renamed.xmp");
        Assert.False(File.Exists(source + ".xmp"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "photo.xmp")));
        Assert.Equal("full-name sidecar", File.ReadAllText(renamedFullSidecar));
        Assert.Equal("stem sidecar", File.ReadAllText(renamedStemSidecar));

        var result = service.Revert(entry);

        Assert.NotNull(result);
        Assert.Equal(source, result!.ToPath);
        Assert.Equal("full-name sidecar", File.ReadAllText(source + ".xmp"));
        Assert.Equal("stem sidecar", File.ReadAllText(Path.Combine(temp.Path, "photo.xmp")));
        Assert.False(File.Exists(renamedFullSidecar));
        Assert.False(File.Exists(renamedStemSidecar));
    }

    [Fact]
    public void Commit_WhenTargetSidecarExists_UsesConflictSuffix()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        File.WriteAllText(source + ".xmp", "source sidecar");
        File.WriteAllText(Path.Combine(temp.Path, "renamed.jpg.xmp"), "existing sidecar");
        var expected = Path.Combine(temp.Path, "renamed (2).jpg");
        var service = new RenameService();

        var result = service.Commit(source, "renamed", ".jpg");

        Assert.Equal(expected, result);
        Assert.Equal("source", File.ReadAllText(expected));
        Assert.Equal("source sidecar", File.ReadAllText(expected + ".xmp"));
        Assert.Equal("existing sidecar", File.ReadAllText(Path.Combine(temp.Path, "renamed.jpg.xmp")));
    }

    [Fact]
    public void Commit_WhenOnlyCasingChanges_PerformsRenameAndTracksUndo()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var service = new RenameService();

        var result = service.Commit(source, "PHOTO", ".jpg");

        Assert.Equal(Path.Combine(temp.Path, "PHOTO.jpg"), result);
        Assert.Equal("PHOTO.jpg", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(temp.Path, "*.jpg"))));
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
