using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ObjectDetectionServiceTests
{
    [Fact]
    public void Decode_MapsReviewedYoloXGridAndCocoClass()
    {
        var output = new float[8400 * 85];
        output[0] = 1;
        output[1] = 1;
        output[2] = MathF.Log(2);
        output[3] = MathF.Log(2);
        output[4] = 0.9f;
        output[5 + 16] = 0.8f;

        var candidate = Assert.Single(ObjectDetectionService.Decode(output, 0.35f));

        Assert.Equal(16, candidate.ClassId);
        Assert.Equal(0.72f, candidate.Confidence, 4);
        Assert.Equal(0, candidate.Left, 4);
        Assert.Equal(16, candidate.Right, 4);
    }

    [Fact]
    public void Decode_RejectsUnexpectedOutputShape()
    {
        Assert.Throws<InvalidDataException>(() => ObjectDetectionService.Decode([1f], 0.35f));
    }

    [Fact]
    public void Nms_IsClassAware()
    {
        YoloXCandidate[] candidates =
        [
            new(0, 0.9f, 0, 0, 100, 100),
            new(0, 0.8f, 5, 5, 105, 105),
            new(1, 0.7f, 5, 5, 105, 105),
        ];

        var selected = ObjectDetectionService.ApplyClassAwareNms(candidates, 0.5f);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, item => item.ClassId == 0 && item.Confidence == 0.9f);
        Assert.Contains(selected, item => item.ClassId == 1);
    }

    [Fact]
    public void InputTensor_UsesRgbAndOfficial114LetterboxFill()
    {
        using var image = new MagickImage(MagickColors.Red, 1, 1);

        var tensor = ObjectDetectionService.BuildInputTensor(image, 1, 1);
        var plane = ObjectDetectionService.InputSize * ObjectDetectionService.InputSize;

        Assert.Equal(255f, tensor[0], 3);
        Assert.Equal(0f, tensor[plane], 3);
        Assert.Equal(0f, tensor[plane * 2], 3);
        Assert.Equal(114f, tensor[1], 3);
    }
}
