using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class FaceCullingHintServiceTests
{
    [Fact]
    public void LaplacianVariance_SeparatesUniformFromSharpPattern()
    {
        var uniform = new float[16, 16];
        var checker = new float[16, 16];
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
            checker[y, x] = (x + y) % 2;

        Assert.Equal(0, FaceCullingHintService.ComputeLaplacianVariance(uniform), 8);
        Assert.True(FaceCullingHintService.ComputeLaplacianVariance(checker) > 1);
    }

    [Fact]
    public void DirectionalTextureRatio_DistinguishesHorizontalLidFromVerticalIrisEdges()
    {
        var horizontalLid = new float[12, 24];
        var verticalIris = new float[12, 24];
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 24; x++)
        {
            horizontalLid[y, x] = y >= 5 && y <= 6 ? 0 : 1;
            verticalIris[y, x] = x >= 10 && x <= 13 ? 0 : 1;
        }

        Assert.True(FaceCullingHintService.ComputeDirectionalTextureRatio(horizontalLid) < 0.1);
        Assert.True(FaceCullingHintService.ComputeDirectionalTextureRatio(verticalIris) > 5);
    }

    [Fact]
    public void ReviewAnalysis_AddsLocalSignalsWithoutChangingReviewDecision()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "face.png");
        using (var image = new MagickImage(MagickColors.Gray, 160, 120))
            image.Write(path, MagickFormat.Png);
        var detection = new FaceDetection(
            30, 20, 100, 90, 0.95,
            [new FaceLandmark(65, 55), new FaceLandmark(100, 55)]);
        FaceRecognitionResult Analyze(string _) => new(
            true, null, path, "CPU",
            [new FaceEmbedding(path, 0, detection, FaceEmbeddingQuality.Accepted, null, [1, 0])]);

        var result = FaceReviewService.Analyze([path], Analyze);
        var candidate = Assert.Single(result.Candidates);

        Assert.NotNull(candidate.CullingHint);
        Assert.True(candidate.CullingHint.PossibleLocalBlur);
        Assert.False(candidate.CullingHint.PossibleClosedEyes);
        var item = new Images.FaceReviewItemViewModel(candidate);
        Assert.Equal(FaceReviewDecision.Pending, item.Decision);
        Assert.Contains("review hint only", item.QualityText, StringComparison.OrdinalIgnoreCase);
    }
}
