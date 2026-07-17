using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class FaceDetectionServiceTests
{
    [Fact]
    public void DecodeStride_ReproducesReviewedYuNetBoxAndLandmarkMath()
    {
        var candidates = FaceDetectionService.DecodeStride(
            stride: 8,
            gridWidth: 1,
            gridHeight: 1,
            classScores: [0.81f],
            objectScores: [1f],
            boxes: [1f, 1f, MathF.Log(2f), MathF.Log(2f)],
            keypoints: [0.5f, 0.5f, 1f, 0.5f, 0.75f, 0.75f, 0.5f, 1f, 1f, 1f],
            confidenceThreshold: 0.8f);

        var face = Assert.Single(candidates);
        Assert.Equal(0f, face.X, 4);
        Assert.Equal(0f, face.Y, 4);
        Assert.Equal(16f, face.Width, 4);
        Assert.Equal(16f, face.Height, 4);
        Assert.Equal(0.9f, face.Confidence, 4);
        Assert.Equal(new FaceLandmark(4, 4), face.Landmarks[0]);
        Assert.Equal(new FaceLandmark(8, 8), face.Landmarks[4]);
    }

    [Fact]
    public void DecodeStride_RejectsUnexpectedModelShapes()
    {
        Assert.Throws<InvalidDataException>(() => FaceDetectionService.DecodeStride(
            8, 2, 2, [1f], [1f], [0f, 0f, 0f, 0f], new float[10], 0.9f));
    }

    [Fact]
    public void NonMaximumSuppression_KeepsHigherConfidenceOverlapAndSeparateFace()
    {
        var landmarks = Enumerable.Repeat(new FaceLandmark(1, 1), 5).ToArray();
        var selected = FaceDetectionService.ApplyNonMaximumSuppression(
        [
            new YuNetCandidate(0, 0, 100, 100, 0.95f, landmarks),
            new YuNetCandidate(5, 5, 100, 100, 0.90f, landmarks),
            new YuNetCandidate(200, 200, 50, 50, 0.85f, landmarks),
        ], threshold: 0.3f, topK: 5000);

        Assert.Equal(2, selected.Count);
        Assert.Equal(0.95f, selected[0].Confidence);
        Assert.Equal(0.85f, selected[1].Confidence);
    }

    [Fact]
    public void FaceDetection_NormalizedAreaUsesMwgCenterCoordinates()
    {
        var face = new FaceDetection(20, 10, 40, 20, 0.9, []);

        Assert.Equal(0.4, face.NormalizedCenterX(100), 6);
        Assert.Equal(0.2, face.NormalizedCenterY(100), 6);
        Assert.Equal(0.4, face.NormalizedWidth(100), 6);
        Assert.Equal(0.2, face.NormalizedHeight(100), 6);
    }

    [Fact]
    public void BuildInputTensor_UsesOpenCvBgrPlanesAndZeroPadding()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 1);
        image.GetPixelsUnsafe().SetPixel(1, 0, [0, 0, Quantum.Max]);

        var tensor = FaceDetectionService.BuildInputTensor(image, 2, 1);
        var plane = FaceDetectionService.InputSize * FaceDetectionService.InputSize;

        Assert.Equal(0f, tensor[0], 3);
        Assert.Equal(255f, tensor[1], 3);
        Assert.Equal(0f, tensor[plane], 3);
        Assert.Equal(0f, tensor[plane + 1], 3);
        Assert.Equal(255f, tensor[plane * 2], 3);
        Assert.Equal(0f, tensor[plane * 2 + 1], 3);
        Assert.Equal(0f, tensor[2], 3);
    }
}
