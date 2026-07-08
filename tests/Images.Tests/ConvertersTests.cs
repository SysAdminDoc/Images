using System.Globalization;
using System.Windows;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class ConvertersTests
{
    [Fact]
    public void BooleanToVisibilityConverter_CollapsesUnsetValueAndNull()
    {
        var converter = new BooleanToVisibilityConverter();

        Assert.Equal(
            Visibility.Collapsed,
            converter.Convert(DependencyProperty.UnsetValue, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(
            Visibility.Collapsed,
            converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToVisibilityConverter_PreservesIntegerCountSemantics()
    {
        var converter = new BooleanToVisibilityConverter();

        Assert.Equal(
            Visibility.Collapsed,
            converter.Convert(0, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(
            Visibility.Visible,
            converter.Convert(1, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(
            Visibility.Visible,
            converter.Convert(-1, typeof(Visibility), null, CultureInfo.InvariantCulture));
    }

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
