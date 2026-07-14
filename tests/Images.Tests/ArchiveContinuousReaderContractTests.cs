using System.IO;

namespace Images.Tests;

public sealed class ArchiveContinuousReaderContractTests
{
    [Fact]
    public void MainWindow_UsesRecyclingLazyArchiveSurface()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "Images", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"ContinuousArchiveReader\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel.CleanUpVirtualizedItem", xaml, StringComparison.Ordinal);
        Assert.Contains("ContinuousArchivePage_Loaded", xaml, StringComparison.Ordinal);
        Assert.Contains("ContinuousArchivePage_Unloaded", xaml, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Images.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
