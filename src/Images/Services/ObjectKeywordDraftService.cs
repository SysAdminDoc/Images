using System.Xml.Linq;

namespace Images.Services;

public static class ObjectKeywordDraftService
{
    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Imv = "https://github.com/SysAdminDoc/Images/ns/1.0/";

    public static XDocument BuildDraft(ObjectDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success)
            throw new ArgumentException("A successful object detection result is required.", nameof(result));
        var tags = result.SuggestedKeywords.Select(tag => new XElement(Rdf + "li", tag));
        return new XDocument(
            new XElement(
                X + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "imv", Imv.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XElement(
                        Rdf + "Description",
                        new XAttribute(Imv + "ObjectTagDraft", "true"),
                        new XElement(Dc + "subject", new XElement(Rdf + "Bag", tags))))));
    }
}
