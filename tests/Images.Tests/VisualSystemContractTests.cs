using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Images.Tests;

public sealed class VisualSystemContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SharedThemeUsesReadableFlatHierarchy()
    {
        var theme = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Images", "Themes", "DarkTheme.xaml"));

        AssertSetter(theme, "ChromeButton", "Background", "Transparent");
        AssertSetter(theme, "ChromeButton", "BorderThickness", "0");
        AssertSetter(theme, "ChromeButton", "FontSize", "15");
        AssertSetter(theme, "Card", "Background", "Transparent");
        AssertSetter(theme, "Card", "BorderThickness", "0");
        AssertSetter(theme, "Badge", "BorderThickness", "0");
        AssertSetter(theme, "ToolbarGroup", "BorderThickness", "0");
        AssertSetter(theme, "InspectorFactTile", "BorderThickness", "0");
        AssertSetter(theme, "Text.Body", "FontSize", "15");
        AssertSetter(theme, "Text.Caption", "FontSize", "13.5");
        AssertSetter(theme, "SectionLabel", "FontSize", "13");
        AssertSetter(theme, "Text.Hint", "FontSize", "13");
        AssertSetter(theme, "StatusCard", "BorderThickness", "2,0,0,0");
        AssertSetter(theme, "ToolWindowHeader", "Padding", "16,8");
        AssertSetter(theme, "ToolWindowHeader", "MinHeight", "48");
        AssertSetter(theme, "ImageContextBar", "MinHeight", "42");

        var sectionLabel = FindStyle(theme, "SectionLabel");
        Assert.DoesNotContain(
            sectionLabel.Elements(Presentation + "Setter"),
            setter => (string?)setter.Attribute("Property") == "Typography.Capitals");

        var tabStyle = theme.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute("TargetType") == "TabItem" && style.Attribute(Xaml + "Key") is null);
        Assert.Equal("0,0,0,2", SetterValue(tabStyle, "BorderThickness"));
        Assert.Equal("15", SetterValue(tabStyle, "FontSize"));
    }

    [Fact]
    public void LiteralTextNeverDropsBelowReadableCaptionSize()
    {
        var sourceRoot = Path.Combine(RepositoryRoot(), "src", "Images");
        var failures = new List<string>();

        foreach (var path in Directory.GetFiles(sourceRoot, "*.xaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (var textBlock in document.Descendants(Presentation + "TextBlock"))
            {
                var rawSize = (string?)textBlock.Attribute("FontSize");
                if (!double.TryParse(rawSize, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var size) || size >= 13)
                    continue;

                var fontFamily = (string?)textBlock.Attribute("FontFamily") ?? "";
                if (fontFamily.Contains("IconFontFamily", StringComparison.Ordinal) ||
                    fontFamily.Contains("Segoe MDL2", StringComparison.Ordinal))
                    continue;

                var line = ((IXmlLineInfo)textBlock).LineNumber;
                failures.Add($"{Path.GetFileName(path)}:{line} uses {size:0.#}pt text.");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void EverySecondaryToolWindowUsesCompactSharedHeader()
    {
        var sourceRoot = Path.Combine(RepositoryRoot(), "src", "Images");
        var exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AboutWindow.xaml",
            "MainWindow.xaml",
            "SettingsWindow.xaml"
        };

        var windows = Directory.GetFiles(sourceRoot, "*Window.xaml")
            .Where(path => !exceptions.Contains(Path.GetFileName(path)))
            .ToArray();

        Assert.NotEmpty(windows);
        foreach (var window in windows)
        {
            var xaml = File.ReadAllText(window);
            Assert.Contains(
                "Style=\"{StaticResource ToolWindowHeader}\"",
                xaml,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MainViewerTracksTheGeneratedPremiumMockupStructure()
    {
        var root = RepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "Images", "MainWindow.xaml"));
        var mockup = new FileInfo(Path.Combine(root, "assets", "mockups", "premium-viewer-v0.3.2-concept.png"));

        Assert.Contains("x:Name=\"WorkflowRail\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ImageContextBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RightSidePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"316\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"InspectorOverview\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource MetaLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource ToolbarGroup}\"", xaml, StringComparison.Ordinal);
        Assert.True(mockup.Exists);
        Assert.True(mockup.Length > 100_000, "The generated implementation reference should be a real high-fidelity raster mockup.");
    }

    private static void AssertSetter(XDocument theme, string key, string property, string expected)
        => Assert.Equal(expected, SetterValue(FindStyle(theme, key), property));

    private static XElement FindStyle(XDocument theme, string key)
        => theme.Descendants(Presentation + "Style")
            .Single(style => (string?)style.Attribute(Xaml + "Key") == key);

    private static string SetterValue(XElement style, string property)
        => style.Elements(Presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == property)
            .Attribute("Value")?.Value
           ?? throw new InvalidDataException($"{property} does not have a literal value.");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Images.sln")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the Images repository root.");
    }
}
