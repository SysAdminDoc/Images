using System.IO;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class DirectoryNavigatorTests
{
    [Fact]
    public void Open_UsesNaturalNameSortByDefault()
    {
        using var temp = TestDirectory.Create();
        var tenth = temp.WriteFile("image10.jpg");
        var second = temp.WriteFile("image2.jpg");
        var first = temp.WriteFile("image1.jpg");

        using var nav = new DirectoryNavigator();

        Assert.True(nav.Open(tenth));
        Assert.Equal(DirectorySortMode.NaturalName, nav.SortMode);
        Assert.Equal([first, second, tenth], nav.Files);
    }

    [Fact]
    public void Open_IncludesZipAndCbzArchiveBooksInFolderNavigation()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("image1.jpg");
        var zip = temp.WriteFile("image2.zip");
        var cbz = temp.WriteFile("image3.cbz");

        using var nav = new DirectoryNavigator();

        Assert.True(nav.Open(image));

        Assert.Equal([image, zip, cbz], nav.Files);
    }

    [Fact]
    public void Open_WithLargeFolder_NaturalSortsAndPreservesTarget()
    {
        using var temp = TestDirectory.Create();
        string? target = null;

        for (var i = 1; i <= 1500; i++)
        {
            var path = temp.WriteFile($"image{i}.jpg");
            if (i == 1000)
                target = path;
        }

        using var nav = new DirectoryNavigator();

        Assert.True(nav.Open(target!));

        Assert.Equal(1500, nav.Count);
        Assert.Equal(target, nav.CurrentPath);
        Assert.EndsWith("image1.jpg", nav.Files[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("image1500.jpg", nav.Files[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetSortMode_ModifiedNewest_PreservesCurrentFileAndReorders()
    {
        using var temp = TestDirectory.Create();
        var old = temp.WriteFile("old.jpg");
        var current = temp.WriteFile("current.jpg");
        var newest = temp.WriteFile("newest.jpg");

        File.SetLastWriteTimeUtc(old, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(current, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newest, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(current));

        Assert.True(nav.SetSortMode(DirectorySortMode.ModifiedNewest));

        Assert.Equal([newest, current, old], nav.Files);
        Assert.Equal(current, nav.CurrentPath);
        Assert.Equal(1, nav.CurrentIndex);
    }

    [Fact]
    public void SetSortMode_ExtensionThenName_GroupsByExtensionWithNaturalNames()
    {
        using var temp = TestDirectory.Create();
        var png10 = temp.WriteFile("frame10.png");
        var jpg = temp.WriteFile("photo.jpg");
        var png2 = temp.WriteFile("frame2.png");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(png10));

        Assert.True(nav.SetSortMode(DirectorySortMode.ExtensionThenName));

        Assert.Equal([jpg, png2, png10], nav.Files);
        Assert.Equal(png10, nav.CurrentPath);
        Assert.Equal(2, nav.CurrentIndex);
    }

    [Fact]
    public void SetSortMode_ExplorerLike_UsesDeterministicNameFallbackAndPreservesCurrentFile()
    {
        using var temp = TestDirectory.Create();
        var png = temp.WriteFile("scan2.png");
        var current = temp.WriteFile("scan10.jpg");
        var webp = temp.WriteFile("scan1.webp");
        var jpg = temp.WriteFile("scan2.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(current));

        Assert.True(nav.SetSortMode(DirectorySortMode.ExplorerLike));

        Assert.Equal([webp, jpg, png, current], nav.Files);
        Assert.Equal(current, nav.CurrentPath);
        Assert.Equal(3, nav.CurrentIndex);
    }

    [Fact]
    public void FolderPreviewController_ShouldPreloadNearbyItemsOnlyInLargeFolders()
    {
        Assert.True(FolderPreviewController.ShouldPreloadThumbnail(count: 9, currentIndex: 4, index: 8));
        Assert.True(FolderPreviewController.ShouldPreloadThumbnail(count: 20, currentIndex: 10, index: 14));
        Assert.True(FolderPreviewController.ShouldPreloadThumbnail(count: 20, currentIndex: 10, index: 6));
        Assert.False(FolderPreviewController.ShouldPreloadThumbnail(count: 20, currentIndex: 10, index: 15));
    }

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
    public void Refresh_AfterVolatileFolderChanges_RescansSupportedFilesAndClampsCurrent()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        var second = temp.WriteFile("b.jpg");
        var current = temp.WriteFile("c.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(current));

        File.Delete(second);
        File.Delete(current);
        var added = temp.WriteFile("d.png");
        temp.WriteFile("notes.txt");

        nav.Refresh();

        Assert.Equal([first, added], nav.Files);
        Assert.Equal(1, nav.CurrentIndex);
        Assert.Equal(added, nav.CurrentPath);
    }

    [Fact]
    public void Refresh_WhenEnumerationFails_KeepsPriorListAndSignalsChange()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        var second = temp.WriteFile("b.jpg");
        var failEnumeration = false;
        using var nav = new DirectoryNavigator(folder =>
        {
            if (failEnumeration)
                throw new IOException("folder temporarily unavailable");

            return Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
        });
        var changes = 0;
        nav.ListChanged += (_, _) => changes++;
        Assert.True(nav.Open(second));

        failEnumeration = true;
        nav.Refresh();

        Assert.Equal(2, changes);
        Assert.Equal([first, second], nav.Files);
        Assert.Equal(second, nav.CurrentPath);
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

    [Fact]
    public void UpdateCurrentPath_WithOldPath_UpdatesMatchingSlotInsteadOfCurrentIndex()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        var second = temp.WriteFile("b.jpg");
        var third = temp.WriteFile("c.jpg");
        var renamedFirst = Path.Combine(temp.Path, "renamed-a.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(first));
        Assert.True(nav.MoveNext());
        Assert.Equal(second, nav.CurrentPath);

        File.Move(first, renamedFirst);
        nav.UpdateCurrentPath(first, renamedFirst);

        Assert.Equal([renamedFirst, second, third], nav.Files);
        Assert.Equal(second, nav.CurrentPath);
    }

    [Fact]
    public void MoveToIndex_JumpsWithinCurrentListWithoutRescanning()
    {
        var scanCount = 0;
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        _ = temp.WriteFile("b.jpg");
        var third = temp.WriteFile("c.jpg");
        using var nav = new DirectoryNavigator(folder =>
        {
            scanCount++;
            return Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
        });

        Assert.True(nav.Open(first));
        Assert.Equal(1, scanCount);

        Assert.True(nav.MoveToIndex(2));

        Assert.Equal(third, nav.CurrentPath);
        Assert.Equal(1, scanCount);
        Assert.False(nav.MoveToIndex(3));
        Assert.Equal(third, nav.CurrentPath);
    }

    [Fact]
    public void RemovePath_RemovesCapturedPathWithoutDroppingCurrentItem()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("a.jpg");
        var second = temp.WriteFile("b.jpg");
        var third = temp.WriteFile("c.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(first));
        Assert.True(nav.MoveNext());
        Assert.Equal(second, nav.CurrentPath);

        Assert.True(nav.RemovePath(first));

        Assert.Equal([second, third], nav.Files);
        Assert.Equal(0, nav.CurrentIndex);
        Assert.Equal(second, nav.CurrentPath);
    }

    [Fact]
    public void GoBack_WhenTopHistoryEntryIsMissing_PopsItAndOpensNextEntry()
    {
        using var parent = TestDirectory.Create();
        var firstDir = Directory.CreateDirectory(Path.Combine(parent.Path, "first")).FullName;
        var secondDir = Directory.CreateDirectory(Path.Combine(parent.Path, "second")).FullName;
        var thirdDir = Directory.CreateDirectory(Path.Combine(parent.Path, "third")).FullName;
        var first = Path.Combine(firstDir, "a.jpg");
        var second = Path.Combine(secondDir, "b.jpg");
        var third = Path.Combine(thirdDir, "c.jpg");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        File.WriteAllText(third, "third");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(first));
        Assert.True(nav.Open(second));
        Assert.True(nav.Open(third));
        File.Delete(second);

        Assert.True(nav.GoBack());

        Assert.Equal(first, nav.CurrentPath);
        Assert.False(nav.CanGoBack);
        Assert.Equal([third], nav.GetForwardHistory());
    }

    [Fact]
    public void ShouldHandleFileSystemEvent_WhenRenamedFromSupportedToUnsupported_ReturnsTrue()
    {
        var args = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            "C:\\images",
            "photo.jpg.bak",
            "photo.jpg");

        Assert.True(DirectoryNavigator.ShouldHandleFileSystemEvent(args));
    }

    [Fact]
    public void OpenExplicitList_SetsFilesAndNavigates()
    {
        using var dir1 = TestDirectory.Create();
        using var dir2 = TestDirectory.Create();
        var a = dir1.WriteFile("a.jpg");
        var b = dir2.WriteFile("b.png");

        using var nav = new DirectoryNavigator();

        Assert.True(nav.OpenExplicitList([a, b]));
        Assert.Equal(2, nav.Count);
        Assert.Equal(a, nav.CurrentPath);
        Assert.Null(nav.Folder);

        nav.MoveNext();
        Assert.Equal(b, nav.CurrentPath);

        nav.MoveNext();
        Assert.Equal(a, nav.CurrentPath);
    }

    [Fact]
    public void OpenExplicitList_FallsBackForSinglePath()
    {
        using var temp = TestDirectory.Create();
        var img = temp.WriteFile("solo.jpg");

        using var nav = new DirectoryNavigator();

        Assert.True(nav.OpenExplicitList([img]));
        Assert.Equal(1, nav.Count);
        Assert.Equal(img, nav.CurrentPath);
    }

    [Fact]
    public void SiblingFolderAutoSwitch_MoveNextAtEnd_SwitchesToNextFolder()
    {
        using var parent = TestDirectory.Create();
        var folderA = Directory.CreateDirectory(Path.Combine(parent.Path, "A"));
        var folderB = Directory.CreateDirectory(Path.Combine(parent.Path, "B"));
        File.WriteAllBytes(Path.Combine(folderA.FullName, "a1.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderA.FullName, "a2.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderB.FullName, "b1.jpg"), [0xFF, 0xD8]);

        using var nav = new DirectoryNavigator();
        nav.SiblingFolderAutoSwitch = true;
        nav.Open(Path.Combine(folderA.FullName, "a1.jpg"));
        nav.MoveNext(); // a2
        Assert.Contains("a2.jpg", nav.CurrentPath);

        nav.MoveNext(); // should switch to folder B
        Assert.Contains("b1.jpg", nav.CurrentPath!);
        Assert.Equal(folderB.FullName, nav.Folder);
    }

    [Fact]
    public void SiblingFolderAutoSwitch_MovePrevAtStart_SwitchesToPreviousFolder()
    {
        using var parent = TestDirectory.Create();
        var folderA = Directory.CreateDirectory(Path.Combine(parent.Path, "A"));
        var folderB = Directory.CreateDirectory(Path.Combine(parent.Path, "B"));
        File.WriteAllBytes(Path.Combine(folderA.FullName, "a1.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderB.FullName, "b1.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderB.FullName, "b2.jpg"), [0xFF, 0xD8]);

        using var nav = new DirectoryNavigator();
        nav.SiblingFolderAutoSwitch = true;
        nav.Open(Path.Combine(folderB.FullName, "b1.jpg"));
        Assert.Contains("b1.jpg", nav.CurrentPath);

        nav.MovePrevious(); // should switch to folder A, last image
        Assert.Contains("a1.jpg", nav.CurrentPath!);
        Assert.Equal(folderA.FullName, nav.Folder);
    }

    [Fact]
    public void SiblingFolderAutoSwitch_Disabled_WrapsNormally()
    {
        using var parent = TestDirectory.Create();
        var folderA = Directory.CreateDirectory(Path.Combine(parent.Path, "A"));
        var folderB = Directory.CreateDirectory(Path.Combine(parent.Path, "B"));
        File.WriteAllBytes(Path.Combine(folderA.FullName, "a1.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderA.FullName, "a2.jpg"), [0xFF, 0xD8]);
        File.WriteAllBytes(Path.Combine(folderB.FullName, "b1.jpg"), [0xFF, 0xD8]);

        using var nav = new DirectoryNavigator();
        nav.SiblingFolderAutoSwitch = false;
        nav.Open(Path.Combine(folderA.FullName, "a1.jpg"));
        nav.MoveNext(); // a2
        nav.MoveNext(); // should wrap to a1
        Assert.Contains("a1.jpg", nav.CurrentPath);
        Assert.Equal(folderA.FullName, nav.Folder);
    }

    [Fact]
    public void OpenExplicitList_SkipsMissingAndUnsupported()
    {
        using var temp = TestDirectory.Create();
        var valid = temp.WriteFile("ok.jpg");
        var missing = Path.Combine(temp.Path, "gone.jpg");
        var unsupported = temp.WriteFile("data.xyz");

        using var nav = new DirectoryNavigator();

        Assert.True(nav.OpenExplicitList([valid, missing, unsupported]));
        Assert.Equal(1, nav.Count);
        Assert.Equal(valid, nav.CurrentPath);
    }

    [Fact]
    public void Open_InExplicitList_DoesNotMatchSameNameFromDifferentDirectory()
    {
        using var parent = TestDirectory.Create();
        var folderA = Directory.CreateDirectory(Path.Combine(parent.Path, "a")).FullName;
        var folderB = Directory.CreateDirectory(Path.Combine(parent.Path, "b")).FullName;
        var inList = Path.Combine(folderA, "IMG_0001.jpg");
        var elsewhere = Path.Combine(folderB, "IMG_0001.jpg");
        var other = Path.Combine(folderA, "IMG_0002.jpg");
        File.WriteAllBytes(inList, [0xFF, 0xD8]);
        File.WriteAllBytes(elsewhere, [0xFF, 0xD8]);
        File.WriteAllBytes(other, [0xFF, 0xD8]);

        using var nav = new DirectoryNavigator();
        Assert.True(nav.OpenExplicitList([inList, other]));

        // Opening a same-named file from a different directory must rescan its
        // own folder, not silently match the in-list entry by filename.
        Assert.True(nav.Open(elsewhere));
        Assert.Equal(elsewhere, nav.CurrentPath);
        Assert.Equal(folderB, nav.Folder);
    }

    [Fact]
    public void StopAtEnds_HaltsAtFirstAndLastWithoutWrapping()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("image1.jpg");
        temp.WriteFile("image2.jpg");
        var last = temp.WriteFile("image3.jpg");

        using var nav = new DirectoryNavigator { StopAtEnds = true };
        Assert.True(nav.Open(first));

        // At the first image, Previous stops (no wrap) and flags the stop.
        Assert.False(nav.MovePrevious());
        Assert.True(nav.LastMoveStoppedAtEnd);
        Assert.Equal(first, nav.CurrentPath);

        // Advancing forward works until the last image.
        Assert.True(nav.MoveNext());
        Assert.True(nav.MoveNext());
        Assert.Equal(last, nav.CurrentPath);
        Assert.False(nav.LastMoveStoppedAtEnd);

        // At the last image, Next stops (no wrap) and flags the stop.
        Assert.False(nav.MoveNext());
        Assert.True(nav.LastMoveStoppedAtEnd);
        Assert.Equal(last, nav.CurrentPath);
    }

    [Fact]
    public void WithoutStopAtEnds_NextWrapsAround()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("image1.jpg");
        temp.WriteFile("image2.jpg");
        var last = temp.WriteFile("image3.jpg");

        using var nav = new DirectoryNavigator();
        Assert.True(nav.Open(last));

        Assert.True(nav.MoveNext());
        Assert.False(nav.LastMoveStoppedAtEnd);
        Assert.Equal(first, nav.CurrentPath);
    }
}
