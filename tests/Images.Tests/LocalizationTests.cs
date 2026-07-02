using System.Globalization;
using System.Reflection;
using System.Resources;
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

    [Fact]
    public void PseudoLocaleResource_ExpandsTextAndPreservesPlaceholders()
    {
        var manager = GetResourceManager();
        var culture = CultureInfo.GetCultureInfo("qps-ploc");

        var cancel = manager.GetString(nameof(Strings.Cancel), culture);
        Assert.NotNull(cancel);
        Assert.StartsWith("[!!", cancel, StringComparison.Ordinal);
        Assert.Contains("Cancel", cancel, StringComparison.Ordinal);
        Assert.True(cancel!.Length > Strings.Cancel.Length);

        var baseFormat = manager.GetString("ModelManagerStorageOpenFailedFormat", CultureInfo.InvariantCulture);
        var pseudoFormat = manager.GetString("ModelManagerStorageOpenFailedFormat", culture);
        Assert.NotNull(baseFormat);
        Assert.NotNull(pseudoFormat);
        Assert.Contains("{0}", pseudoFormat, StringComparison.Ordinal);
        Assert.Equal(FormatPlaceholders(baseFormat!), FormatPlaceholders(pseudoFormat!));
    }

    private static ResourceManager GetResourceManager()
    {
        var field = typeof(Strings).GetField("ResourceManager", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<ResourceManager>(field.GetValue(null));
    }

    private static IReadOnlyList<string> FormatPlaceholders(string value) =>
        System.Text.RegularExpressions.Regex
            .Matches(value, @"\{[0-9]+(?:[^{}]*)\}")
            .Select(match => match.Value)
            .ToArray();
}
