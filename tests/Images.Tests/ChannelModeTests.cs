using Images.Services;

namespace Images.Tests;

public sealed class ChannelModeTests
{
    [Fact]
    public void ChannelMode_HasExactlyFiveMembers()
    {
        var values = Enum.GetValues<ChannelMode>();

        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(ChannelMode.Normal)]
    [InlineData(ChannelMode.Red)]
    [InlineData(ChannelMode.Green)]
    [InlineData(ChannelMode.Blue)]
    [InlineData(ChannelMode.Alpha)]
    public void ChannelMode_ContainsExpectedMember(ChannelMode mode)
    {
        Assert.True(Enum.IsDefined(mode));
    }

    [Fact]
    public void ChannelMode_NormalIsDefault()
    {
        var defaultValue = default(ChannelMode);

        Assert.Equal(ChannelMode.Normal, defaultValue);
    }
}
