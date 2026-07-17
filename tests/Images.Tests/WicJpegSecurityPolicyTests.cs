using Images.Services;

namespace Images.Tests;

public sealed class WicJpegSecurityPolicyTests
{
    [Theory]
    [InlineData(10, 0, 19041, 6215, false)]
    [InlineData(10, 0, 19041, 6216, true)]
    [InlineData(10, 0, 22621, 5767, false)]
    [InlineData(10, 0, 22631, 5768, true)]
    [InlineData(10, 0, 26100, 4945, false)]
    [InlineData(10, 0, 26100, 4946, true)]
    [InlineData(10, 0, 26200, 1, true)]
    public void IsPatched_UsesBranchSpecificAugust2025Floors(
        int major,
        int minor,
        int build,
        int revision,
        bool expected)
    {
        Assert.Equal(expected, WicJpegSecurityPolicy.IsPatched(new Version(major, minor, build, revision)));
    }

    [Fact]
    public void ShouldBypassWic_UnknownVersionOnlyBlocksJpegFamily()
    {
        var unknown = WicJpegSecurityPolicy.Evaluate(null, "probe failed");

        Assert.True(WicJpegSecurityPolicy.ShouldBypassWic("photo.jpeg", unknown));
        Assert.True(WicJpegSecurityPolicy.ShouldBypassWic(".jfif", unknown));
        Assert.False(WicJpegSecurityPolicy.ShouldBypassWic("photo.png", unknown));
    }
}
