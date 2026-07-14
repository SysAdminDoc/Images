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
        var service = CreateService(temp.Path);

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
        var service = CreateService(temp.Path);

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
        var service = CreateService(temp.Path);

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
        var service = CreateService(temp.Path);

        var result = service.Commit(source, "renamed", ".zip");

        Assert.Equal(Path.Combine(temp.Path, "renamed.zip"), result.FinalPath);
        Assert.False(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(result.FinalPath));
    }

    [Fact]
    public void Commit_WhenTargetExists_UsesDeterministicConflictSuffix()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        temp.WriteFile("renamed.jpg", "existing");
        var expected = Path.Combine(temp.Path, "renamed (2).jpg");
        var service = CreateService(temp.Path);

        var result = service.Commit(source, "renamed", ".jpg");

        Assert.Equal(expected, result.FinalPath);
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
        var service = CreateService(temp.Path);

        var renamed = service.Commit(source, "renamed", ".jpg").FinalPath;
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
        var service = CreateService(temp.Path);

        var result = service.Commit(source, "renamed", ".jpg");

        Assert.Equal(expected, result.FinalPath);
        Assert.Equal("source", File.ReadAllText(expected));
        Assert.Equal("source sidecar", File.ReadAllText(expected + ".xmp"));
        Assert.Equal("existing sidecar", File.ReadAllText(Path.Combine(temp.Path, "renamed.jpg.xmp")));
    }

    [Fact]
    public void Commit_WhenOnlyCasingChanges_PerformsRenameAndTracksUndo()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var service = CreateService(temp.Path);

        var result = service.Commit(source, "PHOTO", ".jpg");

        Assert.Equal(Path.Combine(temp.Path, "PHOTO.jpg"), result.FinalPath);
        Assert.Equal("PHOTO.jpg", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(temp.Path, "*.jpg"))));
        Assert.Single(service.UndoHistory);
    }

    [Fact]
    public void Revert_WhenOriginalPathIsOccupied_UsesConflictSuffix()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var service = CreateService(temp.Path);
        var renamed = service.Commit(source, "renamed", ".jpg").FinalPath;
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

    [Fact]
    public void Commit_WhenSidecarMoveFails_RollsBackPrimaryAndLeavesNoRecoveryRecord()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var sidecar = source + ".xmp";
        File.WriteAllText(sidecar, "sidecar");
        var target = Path.Combine(temp.Path, "renamed.jpg");
        var recoveryRoot = Path.Combine(temp.Path, "recovery");
        var fileSystem = new FaultInjectingRenameFileSystem((from, to) =>
            from.Equals(sidecar, StringComparison.OrdinalIgnoreCase) &&
            to.Equals(target + ".xmp", StringComparison.OrdinalIgnoreCase));
        var recovery = new RecoveryCenterService(() => recoveryRoot);
        var service = new RenameService(recovery, fileSystem);

        var error = Assert.Throws<RenameService.RenameTransactionException>(() =>
            service.Commit(source, "renamed", ".jpg"));

        Assert.False(error.IsPartialState);
        Assert.Equal(RenameService.MoveStatus.RolledBack, error.Result.Primary.Status);
        Assert.Contains(error.Result.Sidecars, outcome => outcome.Status == RenameService.MoveStatus.Failed);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(sidecar));
        Assert.False(File.Exists(target));
        Assert.False(File.Exists(target + ".xmp"));
        Assert.Empty(new RecoveryCenterService(() => recoveryRoot).ListRecent());
    }

    [Fact]
    public void Commit_WhenDurableRecoveryIsUnavailable_RollsBackRename()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var target = Path.Combine(temp.Path, "renamed.jpg");
        var service = new RenameService(new RecoveryCenterService(() => null));

        var error = Assert.Throws<RenameService.RenameTransactionException>(() =>
            service.Commit(source, "renamed", ".jpg"));

        Assert.False(error.IsPartialState);
        Assert.Equal(RenameService.MoveStatus.RolledBack, error.Result.Primary.Status);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(target));
        Assert.Empty(service.UndoHistory);
    }

    [Fact]
    public void Commit_WhenRollbackFails_RecordsDurablePartialState()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var fullSidecar = source + ".xmp";
        var stemSidecar = Path.Combine(temp.Path, "photo.xmp");
        File.WriteAllText(fullSidecar, "full sidecar");
        File.WriteAllText(stemSidecar, "stem sidecar");
        var target = Path.Combine(temp.Path, "renamed.jpg");
        var targetFullSidecar = target + ".xmp";
        var targetStemSidecar = Path.Combine(temp.Path, "renamed.xmp");
        var recoveryRoot = Path.Combine(temp.Path, "recovery");
        var fileSystem = new FaultInjectingRenameFileSystem((from, to) =>
            (from.Equals(stemSidecar, StringComparison.OrdinalIgnoreCase) &&
             to.Equals(targetStemSidecar, StringComparison.OrdinalIgnoreCase)) ||
            (from.Equals(targetFullSidecar, StringComparison.OrdinalIgnoreCase) &&
             to.Equals(fullSidecar, StringComparison.OrdinalIgnoreCase)));
        var service = new RenameService(new RecoveryCenterService(() => recoveryRoot), fileSystem);

        var error = Assert.Throws<RenameService.RenameTransactionException>(() =>
            service.Commit(source, "renamed", ".jpg"));

        Assert.True(error.IsPartialState);
        Assert.Equal(RenameService.MoveStatus.RolledBack, error.Result.Primary.Status);
        Assert.Contains(error.Result.Sidecars, outcome => outcome.Status == RenameService.MoveStatus.RollbackFailed);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(targetFullSidecar));
        Assert.True(File.Exists(stemSidecar));
        Assert.False(File.Exists(target));

        var record = Assert.Single(new RecoveryCenterService(() => recoveryRoot).ListRecent());
        Assert.Equal(RecoveryActionRecord.StatusPartial, record.Status);
        Assert.False(record.IsRestorable);
        var recordedSidecar = Assert.Single(record.Sidecars);
        Assert.Equal(fullSidecar, recordedSidecar.OriginalPath);
        Assert.Equal(targetFullSidecar, recordedSidecar.CurrentPath);
    }

    [Fact]
    public void Commit_SuccessfulRenameCanBeRestoredAfterServiceReconstruction()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("photo.jpg", "source");
        var sidecar = source + ".xmp";
        File.WriteAllText(sidecar, "sidecar");
        var recoveryRoot = Path.Combine(temp.Path, "recovery");
        var service = new RenameService(new RecoveryCenterService(() => recoveryRoot));

        var commit = service.Commit(source, "renamed", ".jpg");
        var durable = new RecoveryCenterService(() => recoveryRoot);
        var record = Assert.Single(durable.ListRecent());
        var restored = durable.Restore(record.Id);

        Assert.NotNull(commit.RecoveryRecord);
        Assert.Equal(RenameService.MoveStatus.Moved, commit.Primary.Status);
        Assert.Contains(commit.Sidecars, outcome => outcome.Status == RenameService.MoveStatus.Moved);
        Assert.Equal(RecoveryRestoreStatus.Restored, restored.Status);
        Assert.True(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Equal("sidecar", File.ReadAllText(sidecar));
        Assert.False(File.Exists(commit.FinalPath));
    }

    private static RenameService CreateService(string root)
        => new(new RecoveryCenterService(() => Path.Combine(root, "recovery")));

    private sealed class FaultInjectingRenameFileSystem(Func<string, string, bool> shouldFail) : IRenameFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public void Move(string sourcePath, string destinationPath)
        {
            if (shouldFail(sourcePath, destinationPath))
                throw new IOException("Injected move failure.");

            File.Move(sourcePath, destinationPath, overwrite: false);
        }
    }
}
