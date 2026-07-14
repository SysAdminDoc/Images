using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Images.Services;

/// <summary>
/// Loads user-controlled XML/XMP with explicit resource and entity-expansion limits. Sidecars are
/// discovered beside arbitrary images, so opening a folder must never parse an unbounded document.
/// </summary>
internal static class BoundedXmlReader
{
    internal const long MaxDocumentBytes = 16L * 1024 * 1024;
    private const long MaxDocumentCharacters = 16L * 1024 * 1024;

    internal static XDocument Load(string path, LoadOptions options = LoadOptions.None)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An XML path is required.", nameof(path));

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxDocumentBytes)
            throw new InvalidDataException($"XML document exceeds the {MaxDocumentBytes} byte safety limit.");

        var settings = new XmlReaderSettings
        {
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDocumentCharacters,
            MaxCharactersFromEntities = 1
        };
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, options);
    }
}
