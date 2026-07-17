using System.Globalization;
using System.Xml.Linq;

namespace Images.Services;

public static class FaceMwgRegionService
{
    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace MwgRs = "http://www.metadataworkinggroup.com/schemas/regions/";
    private static readonly XNamespace StArea = "http://ns.adobe.com/xmp/sType/Area#";
    private static readonly XNamespace Imv = "https://github.com/SysAdminDoc/Images/ns/1.0/";

    public static XDocument BuildDraft(FaceDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success)
            throw new ArgumentException("A successful face detection result is required.", nameof(result));

        var regions = result.Faces.Select((face, index) =>
            new XElement(
                Rdf + "li",
                new XAttribute(Rdf + "parseType", "Resource"),
                new XElement(MwgRs + "Name", $"Unassigned face {index + 1}"),
                new XElement(MwgRs + "Type", "Face"),
                new XElement(
                    MwgRs + "Area",
                    new XAttribute(Rdf + "parseType", "Resource"),
                    new XAttribute(StArea + "x", Format(face.NormalizedCenterX(result.ImageWidth))),
                    new XAttribute(StArea + "y", Format(face.NormalizedCenterY(result.ImageHeight))),
                    new XAttribute(StArea + "w", Format(face.NormalizedWidth(result.ImageWidth))),
                    new XAttribute(StArea + "h", Format(face.NormalizedHeight(result.ImageHeight))),
                    new XAttribute(StArea + "unit", "normalized")),
                new XElement(
                    Imv + "DetectionConfidence",
                    face.Confidence.ToString("0.######", CultureInfo.InvariantCulture))))
            .ToArray();

        var bag = new XElement(Rdf + "Bag", regions);
        var regionList = new XElement(MwgRs + "RegionList", bag);
        var regionContainer = new XElement(
            MwgRs + "Regions",
            new XAttribute(Rdf + "parseType", "Resource"),
            regionList);
        var description = new XElement(
            Rdf + "Description",
            new XAttribute(Imv + "FaceRegionDraft", "true"),
            new XAttribute(Imv + "FaceRegionSourceWidth", result.ImageWidth),
            new XAttribute(Imv + "FaceRegionSourceHeight", result.ImageHeight),
            regionContainer);
        var rdf = new XElement(Rdf + "RDF", description);
        var root = new XElement(
            X + "xmpmeta",
            new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "mwg-rs", MwgRs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "stArea", StArea.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "imv", Imv.NamespaceName),
            rdf);
        var document = new XDocument(root);
        return document;
    }

    private static string Format(double value) =>
        Math.Clamp(value, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);
}
