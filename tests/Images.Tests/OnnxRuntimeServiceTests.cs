using System.IO;
using System.Security.Cryptography;
using Images.Services;

namespace Images.Tests;

public sealed class OnnxRuntimeServiceTests
{
    [Fact]
    public void Provider_ReturnsDefinedEnumValue()
    {
        var provider = OnnxRuntimeService.Provider;

        Assert.True(Enum.IsDefined(provider));
    }

    [Fact]
    public void ProviderLabel_IsNonEmpty()
    {
        var label = OnnxRuntimeService.ProviderLabel;

        Assert.False(string.IsNullOrEmpty(label));
    }

    [Fact]
    public void ProviderLabel_MatchesProviderEnum()
    {
        var provider = OnnxRuntimeService.Provider;
        var label = OnnxRuntimeService.ProviderLabel;

        var expected = provider switch
        {
            OnnxProvider.Npu => "NPU",
            OnnxProvider.Gpu => "GPU",
            OnnxProvider.Cpu => "CPU",
            _ => "Unavailable",
        };

        Assert.Equal(expected, label);
    }

    [Fact]
    public void AvailablePaths_AreHardwareLabeledAndIncludeCpuFallback()
    {
        var paths = OnnxRuntimeService.AvailablePaths;

        Assert.NotEmpty(paths);
        Assert.Contains(paths, path => path.Provider == OnnxProvider.Cpu && path.HardwareLabel == "CPU");
        Assert.All(paths, path => Assert.Contains(path.HardwareLabel, new[] { "NPU", "GPU", "CPU" }));
    }

    [Fact]
    public void PinnedAddModel_RunsThroughEveryAvailablePath()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "onnx-test-add.onnx");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modelPath)));
        Assert.Equal("93CF0438706CDDABF683ADC8B13C8A17C4B8B12D8BCCB1B041268E1F4DFF0A2D", hash);

        var validations = OnnxRuntimeService.ValidatePinnedAddModelPaths(modelPath);

        Assert.NotEmpty(validations);
        Assert.All(validations, validation =>
            Assert.True(validation.Success, $"{validation.Runtime.DetailLabel}: {validation.Message}"));
    }
}
