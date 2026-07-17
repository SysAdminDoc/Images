using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class FaceReviewWindowContractTests
{
    [Fact]
    public void Xaml_ExposesVisualReviewAndDisabledWriteGateControls()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Images", "FaceReviewWindow.xaml");
        var document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var names = document.Descendants()
            .Select(element => element.Attribute(x + "Name")?.Value)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("FaceList", names);
        Assert.Contains("PreviewPanel", names);
        Assert.Contains("NameBox", names);
        Assert.Contains("MergeButton", names);
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "ItemsControl" &&
            element.Attribute("ItemsSource")?.Value == "{Binding Overlays}");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attribute(x + "Name")?.Value == "MergeButton");
    }

    [Fact]
    public void ItemViewModel_TracksReviewDecisionNameAndNeverCarriesEmbedding()
    {
        var candidate = new FaceReviewCandidate(
            new FaceReviewKey("photo.jpg", 0),
            new FaceDetection(10, 10, 40, 40, 0.9, []),
            FaceEmbeddingQuality.Accepted,
            null,
            2);
        var item = new Images.FaceReviewItemViewModel(candidate)
        {
            Decision = FaceReviewDecision.Accepted,
            Name = "Ada",
        };

        var review = item.ToReview();

        Assert.Equal(FaceReviewDecision.Accepted, review.Decision);
        Assert.Equal("Ada", review.Name);
        Assert.Contains("2", item.ClusterLabel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            item.GetType().GetProperties(),
            property => property.Name.Contains("Vector", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Images.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
