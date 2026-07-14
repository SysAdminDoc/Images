using System.IO;
using System.Text;
using Images.Services;

namespace Images.Tests;

public sealed class BoundedTextFileReaderTests
{
    [Fact]
    public void ReadUtf8_DetectsBomAndReturnsText()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "preset.json");
        File.WriteAllText(path, "{\"name\":\"Résumé\"}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var text = BoundedTextFileReader.ReadUtf8(path, 1024, "Preset");

        Assert.Equal("{\"name\":\"Résumé\"}", text);
    }

    [Fact]
    public void ReadUtf8_WhenFileExceedsLimit_RejectsBeforeParsing()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "preset.json");
        File.WriteAllBytes(path, new byte[1025]);

        var error = Assert.Throws<InvalidDataException>(() =>
            BoundedTextFileReader.ReadUtf8(path, 1024, "Batch preset"));

        Assert.Contains("1 KiB import limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
