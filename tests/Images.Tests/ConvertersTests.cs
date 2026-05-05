using System.Globalization;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class ConvertersTests
{
    [Fact]
    public void DirectorySortModeMatchesConverter_ReturnsTrueForMatchingParameter()
    {
        var converter = new DirectorySortModeMatchesConverter();

        var result = converter.Convert(
            DirectorySortMode.ModifiedNewest,
            typeof(bool),
            "ModifiedNewest",
            CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    [Fact]
    public void DirectorySortModeMatchesConverter_ReturnsFalseForDifferentParameter()
    {
        var converter = new DirectorySortModeMatchesConverter();

        var result = converter.Convert(
            DirectorySortMode.SizeSmallest,
            typeof(bool),
            "SizeLargest",
            CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }
}
