using System.Globalization;
using System.Xml.Linq;

namespace Images.Services;

internal static class XmpRating
{
    private static readonly XNamespace NsMicrosoftPhoto = "http://ns.microsoft.com/photo/1.0/";

    public static int? Read(XDocument document, int minRating)
    {
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (TryParse(attribute.Name, attribute.Value, minRating, out var rating))
                return rating;
        }

        foreach (var element in document.Descendants())
        {
            if (!element.HasElements && TryParse(element.Name, element.Value, minRating, out var rating))
                return rating;
        }

        return null;
    }

    public static void RemoveAll(XDocument document)
    {
        foreach (var attribute in document.Descendants()
                     .Attributes()
                     .Where(attribute => IsRatingName(attribute.Name))
                     .ToArray())
        {
            attribute.Remove();
        }

        foreach (var element in document.Descendants()
                     .Where(element => IsRatingName(element.Name) && !element.HasElements)
                     .ToArray())
        {
            element.Remove();
        }
    }

    private static bool TryParse(XName name, string value, int minRating, out int rating)
    {
        rating = 0;
        if (!IsRatingName(name) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        rating = name.Namespace == NsMicrosoftPhoto
            ? MapMicrosoftPhotoRating(parsed)
            : Math.Clamp(parsed, minRating, 5);
        return true;
    }

    private static bool IsRatingName(XName name)
        => name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase);

    private static int MapMicrosoftPhotoRating(int rating)
        => rating switch
        {
            >= 99 => 5,
            >= 75 => 4,
            >= 50 => 3,
            >= 25 => 2,
            >= 1 => 1,
            _ => 0
        };
}
