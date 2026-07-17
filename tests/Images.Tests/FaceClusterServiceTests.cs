using Images.Services;

namespace Images.Tests;

public sealed class FaceClusterServiceTests
{
    [Fact]
    public void Cluster_GroupsSimilarEmbeddingsAndKeepsDissimilarFaceSeparate()
    {
        var detection = new FaceDetection(0, 0, 100, 100, 0.95, []);
        FaceEmbedding[] faces =
        [
            new("a.jpg", 0, detection, FaceEmbeddingQuality.Accepted, null, [1, 0]),
            new("b.jpg", 0, detection, FaceEmbeddingQuality.Accepted, null, [0.99f, 0.05f]),
            new("c.jpg", 0, detection, FaceEmbeddingQuality.Accepted, null, [0, 1]),
        ];

        var result = FaceClusterService.Cluster(faces, similarityThreshold: 0.8);

        Assert.Equal(2, result.Clusters.Count);
        Assert.Equal(2, result.Clusters[0].Members.Count);
        Assert.Single(result.Clusters[1].Members);
    }

    [Fact]
    public void Cluster_ReportsQualityRejectedFacesWithoutVectors()
    {
        var detection = new FaceDetection(0, 0, 20, 20, 0.9, []);
        var rejected = new FaceEmbedding(
            "small.jpg", 0, detection, FaceEmbeddingQuality.FaceTooSmall, "too small", null);

        var result = FaceClusterService.Cluster([rejected]);

        Assert.Empty(result.Clusters);
        Assert.Equal(rejected, Assert.Single(result.RejectedFaces));
    }
}
