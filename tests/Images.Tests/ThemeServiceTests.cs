using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Images.Services;
using Images.ViewModels;

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
        var dictionary = LoadThemeDictionary("HighContrastTheme.xaml");

        Assert.True(ThemeService.IsHighContrastDictionary(dictionary));
        Assert.Equal(SystemColors.ControlTextColor, Assert.IsType<SolidColorBrush>(dictionary["TextBrush"]).Color);
        Assert.Equal(SystemColors.HighlightColor, Assert.IsType<SolidColorBrush>(dictionary["AccentBrush"]).Color);
        Assert.Equal(SystemColors.HighlightTextColor, Assert.IsType<SolidColorBrush>(dictionary["CrustBrush"]).Color);
        Assert.Equal(SystemColors.HighlightColor, Assert.IsType<SolidColorBrush>(dictionary["AccentSelectionBrush"]).Color);
        Assert.Equal(SystemColors.WindowColor, Assert.IsType<SolidColorBrush>(dictionary["StatusPanelBrush"]).Color);
    }

    [Fact]
    public void HighContrastEffectiveModeOverridesLatte()
    {
        Assert.Equal(AppThemeMode.HighContrast, ThemeService.ResolveEffectiveMode(AppThemeMode.Latte, highContrast: true));
        Assert.Equal(AppThemeMode.Latte, ThemeService.ResolveEffectiveMode(AppThemeMode.Latte, highContrast: false));
    }

    [Fact]
    public void LatteThemeOverridesSemanticSurfaceBrushes()
    {
        var dark = LoadThemeDictionary("DarkTheme.xaml");
        var latte = LoadThemeDictionary("LatteTheme.xaml");

        Assert.True(ThemeService.IsLatteDictionary(latte));
        Assert.NotEqual(
            Assert.IsType<SolidColorBrush>(dark["PanelBrush"]).Color,
            Assert.IsType<SolidColorBrush>(latte["PanelBrush"]).Color);
        Assert.Equal(Color.FromRgb(248, 250, 252), Assert.IsType<SolidColorBrush>(latte["ViewportBrush"]).Color);
        Assert.Equal(Color.FromArgb(0xFB, 239, 241, 245), Assert.IsType<SolidColorBrush>(latte["FloatingChromeBrush"]).Color);
        Assert.True(latte.Contains("AccentPanelBrush"));
        Assert.True(latte.Contains("AccentSelectionBrush"));
        Assert.True(latte.Contains("SuccessPanelBrush"));
        Assert.True(latte.Contains("OverlayGuideBrush"));
    }

    [Fact]
    public void DarkThemeDefinesCollectionControlChrome()
    {
        var dictionary = LoadThemeDictionary("DarkTheme.xaml");

        Assert.IsType<Style>(dictionary[typeof(DataGrid)]);
        Assert.IsType<Style>(dictionary[typeof(DataGridColumnHeader)]);
        Assert.IsType<Style>(dictionary[typeof(DataGridRow)]);
        Assert.IsType<Style>(dictionary[typeof(DataGridCell)]);
        Assert.IsType<Style>(dictionary[typeof(ListBoxItem)]);
    }

    [Fact]
    public void DarkThemeDefinesGlobalPathConverters()
    {
        var dictionary = LoadThemeDictionary("DarkTheme.xaml");

        Assert.IsType<PathToFileNameConverter>(dictionary["PathToFileNameConverter"]);
        Assert.IsType<PathToParentConverter>(dictionary["PathToParentConverter"]);
    }

    private static ResourceDictionary CreateTestHighContrastDictionary()
        => new() { [ThemeService.ThemeMarkerKey] = ThemeService.HighContrastThemeName };

    private static ResourceDictionary ThrowIfCreated()
        => throw new InvalidOperationException("Duplicate high-contrast dictionaries should not be created.");

    private static ResourceDictionary LoadThemeDictionary(string fileName)
    {
        ResourceDictionary? dictionary = null;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                dictionary = (ResourceDictionary)Application.LoadComponent(
                    new Uri($"/Images;component/Themes/{fileName}", UriKind.Relative));
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
            throw new InvalidOperationException($"{fileName} failed to load.", exception);

        return dictionary ?? throw new InvalidOperationException($"{fileName} did not produce a dictionary.");
    }
}
