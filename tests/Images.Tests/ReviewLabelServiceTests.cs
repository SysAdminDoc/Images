using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class ReviewLabelServiceTests
{
    [Fact]
    public void SetRatingAndLabel_WriteAndReadXmpSidecar()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();

        var rating = service.SetRating(image, 5);
        var label = service.SetLabel(image, ReviewLabelKind.Pick);
        var state = service.ReadState(image);

        Assert.True(rating.Success);
        Assert.True(label.Success);
        Assert.Equal(5, state.Rating);
        Assert.Equal(ReviewLabelKind.Pick, state.Label);
        Assert.True(File.Exists(image + ".xmp"));
        Assert.Contains("ReviewLabel", File.ReadAllText(image + ".xmp"));
    }

    [Fact]
    public void Restore_RevertsPreviousReviewState()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 4);
        var mutation = service.SetLabel(image, ReviewLabelKind.Reject);

        service.Restore(image, mutation.Previous);
        var restored = service.ReadState(image);

        Assert.Equal(4, restored.Rating);
        Assert.Equal(ReviewLabelKind.None, restored.Label);
    }

    [Fact]
    public void SetRating_NullClearsRatingAndPreservesLabel()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "photo.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 3);
        service.SetLabel(image, ReviewLabelKind.Pick);

        service.SetRating(image, null);
        var state = service.ReadState(image);

        Assert.Null(state.Rating);
        Assert.Equal(ReviewLabelKind.Pick, state.Label);
    }

    [Fact]
    public void SetRating_NullClearsElementAndMicrosoftPhotoRatings()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "element-rating.png");
        File.WriteAllText(
            image + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmlns:MicrosoftPhoto="http://ns.microsoft.com/photo/1.0/">
              <rdf:RDF>
                <rdf:Description MicrosoftPhoto:Rating="50">
                  <xmp:Rating>5</xmp:Rating>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """);
        var service = new ReviewLabelService();

        var result = service.SetRating(image, null);
        var state = service.ReadState(image);
        var sidecar = XDocument.Load(image + ".xmp");

        Assert.True(result.Success);
        Assert.Null(state.Rating);
        Assert.DoesNotContain(
            sidecar.Descendants().Attributes(),
            attribute => attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            sidecar.Descendants(),
            element => element.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetColorLabel_WriteAndReadXmpSidecar()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "labeled.png");
        var service = new ReviewLabelService();

        var result = service.SetColorLabel(image, "Green");
        var state = service.ReadState(image);

        Assert.True(result.Success);
        Assert.Equal("Green", state.ColorLabel);
        Assert.Contains("Label", File.ReadAllText(image + ".xmp"));
    }

    [Fact]
    public void SetLocation_WriteAndReadXmpSidecar()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "geotagged.png");
        var service = new ReviewLabelService();
        var location = new SidecarLocation("Central Park", "New York", "NY", "USA");

        var result = service.SetLocation(image, location);
        var state = service.ReadState(image);

        Assert.True(result.Success);
        Assert.Equal("New York", state.Location.City);
        Assert.Equal("NY", state.Location.StateProvince);
        Assert.Equal("USA", state.Location.Country);
        Assert.Equal("Central Park", state.Location.Location);
    }

    [Fact]
    public void SetLocation_EmptyClearsExistingLocationAttributes()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "clear-location.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 5);
        service.SetLocation(image, new SidecarLocation("Central Park", "New York", "NY", "USA"));

        var result = service.SetLocation(image, SidecarLocation.Empty);
        var state = service.ReadState(image);
        var sidecar = XDocument.Load(image + ".xmp");
        XNamespace photoshop = "http://ns.adobe.com/photoshop/1.0/";
        XNamespace iptcCore = "http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/";

        Assert.True(result.Success);
        Assert.Equal("Location cleared.", result.Message);
        Assert.Equal(5, state.Rating);
        Assert.False(state.Location.HasAnyField);
        Assert.DoesNotContain(sidecar.Descendants().Attributes(), attribute => attribute.Name == photoshop + "City");
        Assert.DoesNotContain(sidecar.Descendants().Attributes(), attribute => attribute.Name == photoshop + "State");
        Assert.DoesNotContain(sidecar.Descendants().Attributes(), attribute => attribute.Name == photoshop + "Country");
        Assert.DoesNotContain(sidecar.Descendants().Attributes(), attribute => attribute.Name == iptcCore + "Location");
    }

    [Fact]
    public void SetColorLabel_PreservesExistingRatingAndReviewLabel()
    {
        using var temp = TestDirectory.Create();
        var image = WritePng(temp.Path, "multi.png");
        var service = new ReviewLabelService();
        service.SetRating(image, 4);
        service.SetLabel(image, ReviewLabelKind.Pick);

        service.SetColorLabel(image, "Blue");
        var state = service.ReadState(image);

        Assert.Equal(4, state.Rating);
        Assert.Equal(ReviewLabelKind.Pick, state.Label);
        Assert.Equal("Blue", state.ColorLabel);
    }

    private static string WritePng(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            Enumerable.Repeat((byte)0x80, 16).ToArray(),
            8);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
