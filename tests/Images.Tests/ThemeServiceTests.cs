using System.Windows;
using System.Windows.Media;
using Images.Services;

namespace Images.Tests;

public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldUseHighContrastHonorsAppPreferenceOrSystemFlag(
        bool appPreference,
        bool systemHighContrast,
        bool expected)
    {
        Assert.Equal(expected, ThemeService.ShouldUseHighContrast(appPreference, systemHighContrast));
    }

    [Fact]
    public void SetHighContrastDictionaryAddsOneThemeDictionary()
    {
        var resources = new ResourceDictionary();

        ThemeService.SetHighContrastDictionary(resources, enabled: true, createDictionary: CreateTestHighContrastDictionary);
        ThemeService.SetHighContrastDictionary(resources, enabled: true, createDictionary: ThrowIfCreated);

        var dictionary = Assert.Single(resources.MergedDictionaries);
        Assert.True(ThemeService.IsHighContrastDictionary(dictionary));
    }

    [Fact]
    public void SetHighContrastDictionaryRemovesThemeDictionaryWhenDisabled()
    {
        var resources = new ResourceDictionary();
        ThemeService.SetHighContrastDictionary(resources, enabled: true, createDictionary: CreateTestHighContrastDictionary);

        ThemeService.SetHighContrastDictionary(resources, enabled: false);

        Assert.Empty(resources.MergedDictionaries);
    }

    [Fact]
    public void SetHighContrastDictionaryRefreshReplacesExistingDictionary()
    {
        var resources = new ResourceDictionary();
        ThemeService.SetHighContrastDictionary(resources, enabled: true, createDictionary: CreateTestHighContrastDictionary);
        var original = Assert.Single(resources.MergedDictionaries);

        ThemeService.SetHighContrastDictionary(resources, enabled: true, refresh: true, createDictionary: CreateTestHighContrastDictionary);

        var refreshed = Assert.Single(resources.MergedDictionaries);
        Assert.NotSame(original, refreshed);
        Assert.True(ThemeService.IsHighContrastDictionary(refreshed));
    }

    [Fact]
    public void HighContrastThemeLoadsSystemBrushResources()
    {
        var dictionary = LoadHighContrastThemeDictionary();

        Assert.True(ThemeService.IsHighContrastDictionary(dictionary));
        Assert.Equal(SystemColors.ControlTextColor, Assert.IsType<SolidColorBrush>(dictionary["TextBrush"]).Color);
        Assert.Equal(SystemColors.HighlightColor, Assert.IsType<SolidColorBrush>(dictionary["AccentBrush"]).Color);
        Assert.Equal(SystemColors.HighlightTextColor, Assert.IsType<SolidColorBrush>(dictionary["CrustBrush"]).Color);
    }

    private static ResourceDictionary CreateTestHighContrastDictionary()
        => new() { [ThemeService.ThemeMarkerKey] = ThemeService.HighContrastThemeName };

    private static ResourceDictionary ThrowIfCreated()
        => throw new InvalidOperationException("Duplicate high-contrast dictionaries should not be created.");

    private static ResourceDictionary LoadHighContrastThemeDictionary()
    {
        ResourceDictionary? dictionary = null;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                dictionary = (ResourceDictionary)Application.LoadComponent(
                    new Uri("/Images;component/Themes/HighContrastTheme.xaml", UriKind.Relative));
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw new InvalidOperationException("HighContrastTheme.xaml failed to load.", exception);

        return dictionary ?? throw new InvalidOperationException("HighContrastTheme.xaml did not produce a dictionary.");
    }
}
