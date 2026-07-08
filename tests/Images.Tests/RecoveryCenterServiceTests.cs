using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class RecoveryCenterServiceTests
{
    [Fact]
    public void Restore_QuarantineRecordMovesFileBackToOriginalPath()
    {
        using var temp = TestDirectory.Create();
        var original = Path.Combine(temp.Path, "photo.png");
        var quarantine = Path.Combine(temp.Path, "quarantine", "photo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
        File.WriteAllText(quarantine, "image");
        var service = CreateService(temp.Path);
        var record = service.RecordQuarantine(original, quarantine, "Duplicate quarantine", "Moved by duplicate cleanup.");

        var result = service.Restore(record.Id);

        Assert.Equal(RecoveryRestoreStatus.Restored, result.Status);
        Assert.Equal(original, result.RestoredPath);
        Assert.True(File.Exists(original));
        Assert.False(File.Exists(quarantine));
        Assert.Equal("image", File.ReadAllText(original));
        Assert.Equal(RecoveryActionRecord.StatusRestored, Assert.Single(service.ListRecent()).Status);
    }

    [Fact]
    public void Restore_WhenOriginalExistsUsesConflictSafeRestoredName()
    {
        using var temp = TestDirectory.Create();
        var original = Path.Combine(temp.Path, "photo.png");
        var moved = Path.Combine(temp.Path, "moved", "photo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        File.WriteAllText(original, "existing");
        File.WriteAllText(moved, "moved");
        var service = CreateService(temp.Path);
        var record = service.RecordMove(original, moved, "Moved image", "Moved to another folder.");

        var result = service.Restore(record.Id);

        var restored = Path.Combine(temp.Path, "photo (restored).png");
        Assert.Equal(RecoveryRestoreStatus.Restored, result.Status);
        Assert.Equal(restored, result.RestoredPath);
        Assert.Equal("existing", File.ReadAllText(original));
        Assert.Equal("moved", File.ReadAllText(restored));
    }

    [Fact]
    public void Restore_MoveRecordRestoresSidecarsBesideImage()
    {
        using var temp = TestDirectory.Create();
        var original = Path.Combine(temp.Path, "source", "photo.jpg");
        var moved = Path.Combine(temp.Path, "destination", "photo.jpg");
        var movedSidecar = moved + ".xmp";
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        File.WriteAllText(moved, "image");
        File.WriteAllText(movedSidecar, "sidecar");
        var service = CreateService(temp.Path);
        var record = service.RecordMove(
            original,
            moved,
            "Moved image",
            "Moved image and sidecar.",
            [new RecoverySidecarMove(original + ".xmp", movedSidecar)]);

        var result = service.Restore(record.Id);

        Assert.Equal(RecoveryRestoreStatus.Restored, result.Status);
        Assert.True(File.Exists(original));
        Assert.True(File.Exists(original + ".xmp"));
        Assert.Equal("sidecar", File.ReadAllText(original + ".xmp"));
        Assert.False(File.Exists(movedSidecar));
    }

    [Fact]
    public void Restore_QuarantineRecordRestoresSidecarsBesideImage()
    {
        using var temp = TestDirectory.Create();
        var original = Path.Combine(temp.Path, "source", "photo.jpg");
        var quarantine = Path.Combine(temp.Path, "quarantine", "photo.jpg");
        var quarantineSidecar = quarantine + ".xmp";
        Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
        File.WriteAllText(quarantine, "image");
        File.WriteAllText(quarantineSidecar, "sidecar");
        var service = CreateService(temp.Path);
        var record = service.RecordQuarantine(
            original,
            quarantine,
            "Quarantined image",
            "Moved image and sidecar.",
            [new RecoverySidecarMove(original + ".xmp", quarantineSidecar)]);

        var result = service.Restore(record.Id);

        Assert.Equal(RecoveryRestoreStatus.Restored, result.Status);
        Assert.True(File.Exists(original));
        Assert.True(File.Exists(original + ".xmp"));
        Assert.Equal("sidecar", File.ReadAllText(original + ".xmp"));
        Assert.False(File.Exists(quarantineSidecar));
    }

    [Fact]
    public void Restore_WhenCurrentPathIsMissingMarksRecordMissing()
    {
        using var temp = TestDirectory.Create();
        var service = CreateService(temp.Path);
        var record = service.RecordMove(
            Path.Combine(temp.Path, "source.png"),
            Path.Combine(temp.Path, "missing.png"),
            "Moved image",
            "Moved to another folder.");

        var result = service.Restore(record.Id);

        Assert.Equal(RecoveryRestoreStatus.MissingCurrentPath, result.Status);
        Assert.Equal(RecoveryActionRecord.StatusMissing, Assert.Single(service.ListRecent()).Status);
    }

    [Fact]
    public void Restore_WritebackRecordExplainsThatItIsNotRestorable()
    {
        using var temp = TestDirectory.Create();
        var service = CreateService(temp.Path);
        var record = service.RecordWriteback(
            Path.Combine(temp.Path, "photo.png"),
            "Crop writeback",
            "The file was overwritten by crop.");

        var result = service.Restore(record.Id);

        Assert.Equal(RecoveryRestoreStatus.NotRestorable, result.Status);
        Assert.Contains("No automatic restore", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RecoveryCenterService CreateService(string root)
        => new(() => root, () => new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));
}
