using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ImageExportServiceTests
{
    [Fact]
    public void ResolveWritablePath_WhenExtensionCannotBeWritten_NormalizesAndFallsBackToPng()
    {
        using var temp = TestDirectory.Create();
        var requested = Path.Combine(temp.Path, "nested", "..", "export.not-a-real-format");

        var resolved = ImageExportService.ResolveWritablePath(requested);

        Assert.Equal(Path.Combine(temp.Path, "export.png"), resolved);
    }

    [Fact]
    public void Save_WhenTargetExists_ReplacesAtomically()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("export.png", "old");
        var oldBytes = File.ReadAllBytes(target);
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0xFF, 0x00, 0x00, 0xFF },
            4);
        bitmap.Freeze();

        var savedPath = ImageExportService.Save(bitmap, target);

        Assert.Equal(Path.GetFullPath(target), savedPath);
        Assert.True(new FileInfo(target).Length > 0);
        Assert.NotEqual(oldBytes, File.ReadAllBytes(target));
    }
}
