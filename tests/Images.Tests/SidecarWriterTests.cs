using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class SidecarWriterTests
{
    [Fact]
    public void SaveAtomically_UsesGuidTempFileAndLeavesFixedTempUntouched()
    {
        using var temp = TestDirectory.Create();
        var sidecarPath = Path.Combine(temp.Path, "photo.jpg.xmp");
        var fixedTempPath = sidecarPath + ".images-tmp";
        File.WriteAllText(fixedTempPath, "sentinel");

        SidecarWriter.SaveAtomically(
            new XDocument(new XElement("xmp", new XElement("rating", "5"))),
            sidecarPath);

        Assert.True(File.Exists(sidecarPath));
        Assert.Equal("sentinel", File.ReadAllText(fixedTempPath));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "photo.jpg.xmp.*.images-tmp"));
    }

    [Fact]
    public async Task SaveAtomically_ParallelWritesDoNotShareTempFilesOrLeaveDebris()
    {
        using var temp = TestDirectory.Create();
        var sidecarPath = Path.Combine(temp.Path, "photo.jpg.xmp");
        File.WriteAllText(sidecarPath, "<xmp><value>seed</value></xmp>");

        var tasks = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() =>
                SidecarWriter.SaveAtomically(
                    new XDocument(
                        new XElement(
                            "xmp",
                            new XElement("value", index.ToString(CultureInfo.InvariantCulture)))),
                    sidecarPath)));

        await Task.WhenAll(tasks);

        var document = XDocument.Load(sidecarPath);
        var value = int.Parse(document.Root!.Element("value")!.Value, CultureInfo.InvariantCulture);
        Assert.InRange(value, 0, 31);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.images-tmp"));
    }
}
