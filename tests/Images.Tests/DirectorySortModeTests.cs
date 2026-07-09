using Images.Services;

namespace Images.Tests;

public sealed class DirectorySortModeTests
{
    [Theory]
    [InlineData("NaturalName", true, DirectorySortMode.NaturalName)]
    [InlineData("ExplorerLike", true, DirectorySortMode.ExplorerLike)]
    [InlineData("ModifiedNewest", true, DirectorySortMode.ModifiedNewest)]
    [InlineData("extensionthenname", true, DirectorySortMode.ExtensionThenName)]
    [InlineData("invalid", false, DirectorySortMode.NaturalName)]
    public void TryParseCommandParameter_StringRoundTrip(string input, bool expectedSuccess, DirectorySortMode expectedMode)
    {
        var success = DirectorySortModeInfo.TryParseCommandParameter(input, out var mode);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void TryParseCommandParameter_AcceptsEnumValue()
    {
        var success = DirectorySortModeInfo.TryParseCommandParameter(DirectorySortMode.SizeLargest, out var mode);

        Assert.True(success);
        Assert.Equal(DirectorySortMode.SizeLargest, mode);
    }

    [Fact]
    public void All_ContainsEveryEnumValue()
    {
        var allModes = DirectorySortModeInfo.All;
        var enumValues = Enum.GetValues<DirectorySortMode>();

        Assert.Equal(enumValues.Length, allModes.Count);
        foreach (var val in enumValues)
            Assert.Contains(val, allModes);
    }

    [Theory]
    [InlineData(DirectorySortMode.NaturalName, "Name (natural)")]
    [InlineData(DirectorySortMode.ExplorerLike, "Explorer-like name order")]
    [InlineData(DirectorySortMode.SizeLargest, "Size (largest first)")]
    [InlineData(DirectorySortMode.ExtensionThenName, "Type, then name")]
    public void DisplayName_ReturnsExpected(DirectorySortMode mode, string expected)
    {
        Assert.Equal(expected, DirectorySortModeInfo.DisplayName(mode));
    }

    [Theory]
    [InlineData(DirectorySortMode.NaturalName)]
    [InlineData(DirectorySortMode.ExplorerLike)]
    [InlineData(DirectorySortMode.ModifiedNewest)]
    [InlineData(DirectorySortMode.SizeSmallest)]
    public void ShortLabel_IsNonEmpty(DirectorySortMode mode)
    {
        var label = DirectorySortModeInfo.ShortLabel(mode);

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.StartsWith("Sort:", label);
    }
}
