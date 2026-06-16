using Images.Localization;

namespace Images.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void Strings_ReturnsDefaultEnglishResource()
    {
        Assert.Equal("Confirm action", Strings.ConfirmAction);
        Assert.Equal("Cancel", Strings.Cancel);
    }

    [Fact]
    public void Strings_UnknownKeyReturnsVisibleMissingMarker()
    {
        Assert.Equal("!Missing.Localization.Key!", Strings.Get("Missing.Localization.Key"));
    }

    [Fact]
    public void LocExtension_ReturnsResourceValue()
    {
        var extension = new LocExtension("ConfirmAction");

        Assert.Equal("Confirm action", extension.ProvideValue(serviceProvider: null!));
    }
}
