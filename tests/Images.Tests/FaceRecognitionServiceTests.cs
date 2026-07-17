using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class FaceRecognitionServiceTests
{
    [Fact]
    public void SimilarityTransform_MapsMatchingLandmarksToIdentity()
    {
        FaceLandmark[] points =
        [
            new(10, 20), new(30, 20), new(20, 30), new(12, 40), new(28, 40),
        ];

        var transform = FaceRecognitionService.ComputeSimilarityTransform(points, points);

        Assert.Equal(1, transform.A, 8);
        Assert.Equal(0, transform.B, 8);
        Assert.Equal(0, transform.TranslateX, 8);
        Assert.Equal(0, transform.TranslateY, 8);
    }

    [Fact]
    public void SimilarityTransform_RejectsDegenerateLandmarks()
    {
        var repeated = Enumerable.Repeat(new FaceLandmark(1, 1), 5).ToArray();

        Assert.Throws<ArgumentException>(() =>
            FaceRecognitionService.ComputeSimilarityTransform(repeated));
    }

    [Fact]
    public void CosineSimilarity_DistinguishesIdenticalAndOrthogonalVectors()
    {
        Assert.Equal(1, FaceRecognitionService.CosineSimilarity([1, 0], [1, 0]), 8);
        Assert.Equal(0, FaceRecognitionService.CosineSimilarity([1, 0], [0, 1]), 8);
    }

    [Fact]
    public void AlignedTensor_UsesRgbPlanesForIdentityTransform()
    {
        using var image = new MagickImage(MagickColors.Red, 112, 112);
        FaceLandmark[] template =
        [
            new(38.2946, 51.6963), new(73.5318, 51.5014), new(56.0252, 71.7366),
            new(41.5493, 92.3655), new(70.7299, 92.2041),
        ];

        Assert.True(FaceRecognitionService.TryBuildAlignedTensor(image, template, out var tensor));
        var plane = FaceRecognitionService.AlignedSize * FaceRecognitionService.AlignedSize;
        Assert.Equal(255f, tensor[0], 3);
        Assert.Equal(0f, tensor[plane], 3);
        Assert.Equal(0f, tensor[plane * 2], 3);
    }
}
