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
            OnnxProvider.DirectML => "DirectML",
            OnnxProvider.Cpu => "CPU",
            _ => "Unavailable",
        };

        Assert.Equal(expected, label);
    }
}
