using System.IO;
using System.Xml;
using Images.Services;

namespace Images.Tests;

public sealed class BoundedXmlReaderTests
{
    [Fact]
    public void Load_OrdinaryXmp_ReturnsDocument()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("photo.xmp", "<xmpmeta><rating>5</rating></xmpmeta>");

        var document = BoundedXmlReader.Load(path);

        Assert.Equal("xmpmeta", document.Root?.Name.LocalName);
        Assert.Equal("5", document.Root?.Element("rating")?.Value);
    }

    [Fact]
    public void Load_DtdDocument_RejectsEntityExpansion()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile(
            "entities.xmp",
            "<!DOCTYPE x [<!ENTITY payload 'expanded'>]><x>&payload;</x>");

        Assert.Throws<XmlException>(() => BoundedXmlReader.Load(path));
    }

    [Fact]
    public void Load_OversizedDocument_RejectsBeforeParsing()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "oversized.xmp");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.SetLength(BoundedXmlReader.MaxDocumentBytes + 1);

        Assert.Throws<InvalidDataException>(() => BoundedXmlReader.Load(path));
    }
}
