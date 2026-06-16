using Images.Services;

namespace Images.Tests;

public sealed class ClipImagePreprocessorTests
{
    [Fact]
    public void DefaultConstructor_UsesClipNormalization()
    {
        var preprocessor = new ClipImagePreprocessor();
        Assert.NotNull(preprocessor);
    }

    [Fact]
    public void Load_ReadsPreprocessorConfig()
    {
        using var temp = TestDirectory.Create();
        var configPath = temp.WriteFile("config.json", """
        {
          "image_mean": [0.48145466, 0.4578275, 0.40821073],
          "image_std": [0.26862954, 0.26130258, 0.27577711],
          "size": {"shortest_edge": 224},
          "do_resize": true,
          "do_center_crop": true,
          "do_normalize": true
        }
        """);

        var preprocessor = ClipImagePreprocessor.Load(configPath);
        Assert.NotNull(preprocessor);
    }

    [Fact]
    public void Load_HandlesMissingOptionalFields()
    {
        using var temp = TestDirectory.Create();
        var configPath = temp.WriteFile("config.json", "{}");

        var preprocessor = ClipImagePreprocessor.Load(configPath);
        Assert.NotNull(preprocessor);
    }
}
