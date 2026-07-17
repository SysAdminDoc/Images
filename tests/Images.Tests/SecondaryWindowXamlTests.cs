using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using Images.Localization;
using IOPath = System.IO.Path;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class SecondaryWindowXamlTests
{
    private static readonly string[] ThemeFiles = ["DarkTheme.xaml", "LatteTheme.xaml", "HighContrastTheme.xaml"];

    private static readonly IReadOnlyDictionary<string, string[]> StatusWindowContract =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AboutWindow.xaml"] = ["OcrStatusText", "UpdateStatusText"],
            ["AdjustmentsWindow.xaml"] = ["StatusText"],
            ["AnnotationsWindow.xaml"] = ["StatusText", "ToolStatusText"],
            ["BatchProcessorWindow.xaml"] = ["StatusText"],
            ["DuplicateCleanupWindow.xaml"] = ["StatusText"],
            ["EditStackWindow.xaml"] = ["StatusText"],
            ["EffectsWindow.xaml"] = ["StatusText"],
            ["ExportPreviewWindow.xaml"] = ["C2paStatusText", "StatusText"],
            ["FileHealthScanWindow.xaml"] = ["StatusText"],
            ["FaceReviewWindow.xaml"] = ["StatusValueText"],
            ["ImportInboxWindow.xaml"] = ["StatusText"],
            ["MacroActionWindow.xaml"] = ["StatusText"],
            ["ModelManagerWindow.xaml"] = ["DetailStatus", "RuntimeStatusText", "StatusText"],
            ["PerspectiveCorrectionWindow.xaml"] = ["StatusText"],
            ["RecoveryCenterWindow.xaml"] = ["StatusText", "StatusValueText"],
            ["ReferenceBoardWindow.xaml"] = ["StatusText"],
            ["ResizeDialogWindow.xaml"] = ["StatusText"],
            ["SemanticSearchWindow.xaml"] = ["IndexStatusText", "ProviderStatusText", "StatusText"],
            ["TagGraphWindow.xaml"] = ["StatusText"],
        };

    private sealed record WindowContractCase(
        string ExpectedTitle,
        Func<Window> CreateWindow,
        string[] RequiredAutomationNames);

    [Fact]
    public void EveryNamedSecondaryStatusSurfaceIsAnAnnouncingLiveRegion()
    {
        var sourceDirectory = IOPath.Combine(RepositoryRoot(), "src", "Images");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        var discoveredStatusWindows = Directory
            .EnumerateFiles(sourceDirectory, "*Window.xaml", SearchOption.TopDirectoryOnly)
            .Select(path => (Path: path, Document: XDocument.Load(path)))
            .Where(item => item.Document.Descendants().Any(element =>
                element.Name.LocalName == nameof(TextBlock) &&
                Regex.IsMatch(element.Attribute(xaml + "Name")?.Value ?? "", "Status", RegexOptions.CultureInvariant)))
            .Select(item => IOPath.GetFileName(item.Path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(StatusWindowContract.Keys.Order(StringComparer.Ordinal), discoveredStatusWindows);

        foreach (var xamlPath in Directory.EnumerateFiles(sourceDirectory, "*Window.xaml", SearchOption.TopDirectoryOnly))
        {
            var document = XDocument.Load(xamlPath);
            var liveRegions = document.Descendants()
                .Where(element => element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "AutomationProperties.LiveSetting"))
                .ToArray();

            foreach (var liveRegion in liveRegions)
            {
                var announcesChanges = liveRegion.Attributes()
                    .SingleOrDefault(attribute => attribute.Name.LocalName == "LiveRegionBehavior.AnnounceChanges")
                    ?.Value;
                Assert.True(
                    string.Equals(announcesChanges, "True", StringComparison.OrdinalIgnoreCase),
                    $"{IOPath.GetFileName(xamlPath)} {liveRegion.Name.LocalName} live region must raise change events.");
            }
        }

        foreach (var (fileName, expectedNames) in StatusWindowContract)
        {
            var document = XDocument.Load(IOPath.Combine(sourceDirectory, fileName));
            var statusElements = document.Descendants()
                .Where(element => element.Name.LocalName == nameof(TextBlock))
                .Where(element => expectedNames.Contains(element.Attribute(xaml + "Name")?.Value, StringComparer.Ordinal))
                .ToArray();

            Assert.Equal(expectedNames.Order(StringComparer.Ordinal), statusElements
                .Select(element => element.Attribute(xaml + "Name")!.Value)
                .Order(StringComparer.Ordinal));

            foreach (var statusElement in statusElements)
            {
                var name = statusElement.Attribute(xaml + "Name")!.Value;
                var liveSetting = statusElement.Attributes()
                    .SingleOrDefault(attribute => attribute.Name.LocalName == "AutomationProperties.LiveSetting")
                    ?.Value;
                Assert.True(
                    liveSetting is "Polite" or "Assertive",
                    $"{fileName}.{name} must declare a Polite or Assertive live setting.");

                var announcesChanges = statusElement.Attributes()
                    .SingleOrDefault(attribute => attribute.Name.LocalName == "LiveRegionBehavior.AnnounceChanges")
                    ?.Value;
                Assert.Equal("True", announcesChanges);
            }
        }
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void ThemedControlStylesReplaceDefaultChrome()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var createdApplication = Application.Current is null;
                var application = Application.Current ?? new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                AssertCustomButtonTemplate(
                    "BookPageTurnButton",
                    ["HitZone", "HoverOverlay", "FocusBorder"]);
                AssertCustomButtonTemplate(
                    "AnnotationColorSwatchButton",
                    ["Swatch"],
                    Brushes.Red);
                AssertCustomButtonTemplate(
                    "NavRailButton",
                    ["Bd"]);
                AssertCustomButtonTemplate(
                    "ToolbarTextButton",
                    ["Bd"]);
                AssertVerticalGridSplitterTemplate();
                AssertThemedPasswordBoxTemplate();

                if (createdApplication)
                    application.Shutdown();
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
            throw new InvalidOperationException("Themed button style smoke failed.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void PremiumShellResourcesExistInEveryTheme()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                foreach (var themeFileName in new[] { "DarkTheme.xaml", "LatteTheme.xaml", "HighContrastTheme.xaml" })
                {
                    var dictionary = LoadThemeDictionary(themeFileName);
                    Assert.IsType<SolidColorBrush>(dictionary["NavRailBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["WorkspaceTopBarBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["WorkspaceTopBarBorderBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["InspectorPanelBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["AccentWarmBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["AccentWarmPanelBrush"]);
                    Assert.IsType<SolidColorBrush>(dictionary["OnAccentBrush"]);
                }
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
            throw new InvalidOperationException("Premium shell resource smoke failed.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void FilledButtonTextMeetsAaContrastInCustomThemes()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                foreach (var themeFileName in new[] { "DarkTheme.xaml", "LatteTheme.xaml", "HighContrastTheme.xaml" })
                {
                    AssertThemeContrast(themeFileName, "OnAccentBrush", "AccentBrush", minimumRatio: 4.5);
                    AssertThemeContrast(themeFileName, "OnAccentBrush", "RedBrush", minimumRatio: 4.5);
                }
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
            throw new InvalidOperationException("Filled button contrast smoke failed.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void CodeBehindStatusBrushTracksLiveThemeResourceChanges()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var createdApplication = Application.Current is null;
                var application = Application.Current ?? new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                var firstBrush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
                var secondBrush = new SolidColorBrush(Color.FromRgb(4, 5, 6));
                application.Resources["AccentBrush"] = firstBrush;

                var window = new ImportInboxWindow();
                try
                {
                    var statusType = typeof(ImportInboxWindow).GetNestedType("ImportInboxStatus", BindingFlags.NonPublic);
                    Assert.NotNull(statusType);
                    var busyStatus = Enum.Parse(statusType!, "Busy");
                    var setStatus = typeof(ImportInboxWindow).GetMethod(
                            "SetStatus",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.NotNull(setStatus);

                    setStatus!.Invoke(window, ["Indexing", busyStatus]);
                    var statusDot = Assert.IsAssignableFrom<Shape>(window.FindName("StatusDot"));
                    Assert.Same(firstBrush, statusDot.Fill);

                    application.Resources["AccentBrush"] = secondBrush;
                    Assert.Same(secondBrush, statusDot.Fill);
                }
                finally
                {
                    window.Close();
                    application.Resources.Remove("AccentBrush");
                }

                if (createdApplication)
                    application.Shutdown();
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
            throw new InvalidOperationException("Code-behind status brush theme tracking smoke failed.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void HintTextBrushMeetsAaContrastInCustomThemes()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                AssertThemeContrast("DarkTheme.xaml", "HintTextBrush", "BaseBrush", minimumRatio: 4.5);
                AssertThemeContrast("LatteTheme.xaml", "HintTextBrush", "BaseBrush", minimumRatio: 4.5);

                var highContrast = LoadThemeDictionary("HighContrastTheme.xaml");
                Assert.True(highContrast.Contains("HintTextBrush"));
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
            throw new InvalidOperationException("Hint text contrast smoke failed.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void SecondaryWindowsOpenAndExposeAutomationContract()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_SMOKE_TESTS")))
            Assert.Skip("Set RUN_SMOKE_TESTS=1 to run secondary WPF window smoke tests.");

        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var createdApplication = Application.Current is null;
                var application = Application.Current ?? new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                foreach (var themeFile in ThemeFiles)
                {
                    application.Resources.MergedDictionaries.Clear();
                    application.Resources.MergedDictionaries.Add(LoadThemeDictionary(themeFile));

                    foreach (var testCase in CreateWindowContractCases())
                        AssertWindowContract(testCase);
                }

                if (createdApplication)
                    application.Shutdown();
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
            throw new InvalidOperationException("Secondary window XAML failed to construct.", exception);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void SecondaryWindowsPseudoLocaleDoesNotClipCriticalText()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_SMOKE_TESTS")))
            Assert.Skip("Set RUN_SMOKE_TESTS=1 to run secondary WPF window smoke tests.");

        Exception? exception = null;

        var thread = new Thread(() =>
        {
            var previousStringsCulture = Strings.Culture;
            var previousCurrentCulture = CultureInfo.CurrentCulture;
            var previousCurrentUICulture = CultureInfo.CurrentUICulture;

            try
            {
                var pseudoCulture = CultureInfo.GetCultureInfo("qps-ploc");
                Strings.Culture = pseudoCulture;
                CultureInfo.CurrentCulture = pseudoCulture;
                CultureInfo.CurrentUICulture = pseudoCulture;

                var createdApplication = Application.Current is null;
                var application = Application.Current ?? new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                foreach (var themeFile in ThemeFiles)
                {
                    application.Resources.MergedDictionaries.Clear();
                    application.Resources.MergedDictionaries.Add(LoadThemeDictionary(themeFile));

                    foreach (var testCase in CreateWindowContractCases())
                        AssertPseudoWindowContract(testCase);
                }

                if (createdApplication)
                    application.Shutdown();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Strings.Culture = previousStringsCulture;
                CultureInfo.CurrentCulture = previousCurrentCulture;
                CultureInfo.CurrentUICulture = previousCurrentUICulture;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw new InvalidOperationException("Secondary window pseudo-locale layout smoke failed.", exception);
    }

    private static IReadOnlyList<WindowContractCase> CreateWindowContractCases() =>
    [
        new(
            Strings.SettingsWindowTitle,
            static () => new Images.SettingsWindow(),
            [
                Strings.SettingsHeading,
                Strings.SettingsLanguageTitle,
                Strings.SettingsRememberWindowTitle,
                Strings.SettingsCloseAutomationName
            ]),
        new(
            Strings.AboutTitle,
            static () => new Images.AboutWindow(),
            [
                Strings.AboutDiagnosticsAutomationName,
                Strings.AboutCopySystemInfoAutomationName,
                Strings.AboutCopyCodecReportAutomationName,
                Strings.AboutNetworkActivityAutomationName
            ]),
        new(
            Strings.DuplicateCleanupWindowTitle,
            static () => new Images.DuplicateCleanupWindow(),
            [
                Strings.DuplicateCleanupAddFolderAutomationName,
                Strings.DuplicateCleanupReferenceFolderAutomationName,
                Strings.DuplicateCleanupScanAutomationName,
                Strings.DuplicateCleanupFindingsAutomationName
            ]),
        new(
            Strings.SemanticSearchWindowTitle,
            static () => new Images.SemanticSearchWindow(),
            [
                Strings.SemanticSearchAddFolder,
                Strings.SemanticSearchIndex,
                Strings.SemanticSearchIndexedFolders,
                Strings.SemanticSearchSearch
            ]),
        new(
            Strings.ModelManagerWindowTitle,
            static () => new Images.ModelManagerWindow(),
            [
                Strings.ModelManagerRevealStorageAutomationName,
                Strings.ModelManagerImportAutomationName,
                "Validate CLIP pipeline runtime",
                Strings.ModelManagerApprovedLocalModelsAutomationName
            ]),
        new(
            Strings.Get("FaceReviewWindowTitle"),
            static () => new Images.FaceReviewWindow(),
            [
                Strings.Get("FaceReviewAnalyzeCurrent"),
                Strings.Get("FaceReviewAnalyzeFolder"),
                Strings.Get("FaceReviewDetectedRegions"),
                Strings.Get("FaceReviewMergeSidecars")
            ]),
        new(
            Strings.ImportInboxWindowTitle,
            static () => new Images.ImportInboxWindow(),
            [
                Strings.ImportInboxAddFiles,
                Strings.ImportInboxAddFolder,
                Strings.ImportInboxImportPicasa,
                Strings.ImportInboxChoose,
                Strings.ImportInboxStagedFiles
            ]),
        new(Strings.AdjustmentsWindowTitle, static () => new Images.AdjustmentsWindow(SmokeImagePath(), static _ => { }), []),
        new(Strings.Get("AnnotationTitle"), static () => new Images.AnnotationsWindow(SmokeImagePath(), static _ => { }), []),
        new(Strings.BatchTitle, static () => new Images.BatchProcessorWindow(), []),
        new(Strings.Get("EditHistoryTitle"), static () => new Images.EditStackWindow(), []),
        new(Strings.Get("EffectsWindowTitle"), static () => new Images.EffectsWindow(SmokeImagePath(), static _ => { }), []),
        new(Strings.ExportPreviewWindowTitle, static () => new Images.ExportPreviewWindow(SmokeBitmap(), null, ".png"), []),
        new(Strings.FileHealthScanWindowTitle, static () => new Images.FileHealthScanWindow(), []),
        new(Strings.MacroWindowTitle, static () => new Images.MacroActionWindow(), []),
        new(Strings.Get("PerspectiveTitle"), static () => new Images.PerspectiveCorrectionWindow(SmokeImagePath(), static _ => { }), []),
        new(Strings.RecoveryCenterTitle, static () => new Images.RecoveryCenterWindow(), []),
        new(Strings.ReferenceBoardWindowTitle, static () => new Images.ReferenceBoardWindow(), []),
        new(Strings.Get("ResizeTitle"), static () => new Images.ResizeDialogWindow(640, 480), []),
        new(Strings.TagGraphWindowTitle, static () => new Images.TagGraphWindow(), [])
    ];

    private static string SmokeImagePath()
        => IOPath.Combine(AppContext.BaseDirectory, "Fixtures", "smoke-test.png");

    private static BitmapSource SmokeBitmap()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0, 0, 0, 255 },
            4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void AssertCustomButtonTemplate(string styleKey, string[] requiredTemplateParts, Brush? background = null)
    {
        var style = Assert.IsType<Style>(Application.Current!.Resources[styleKey]);
        var button = new Button
        {
            Style = style,
            Background = background ?? Brushes.Transparent,
            Content = new TextBlock { Text = "Template probe" }
        };

        button.ApplyTemplate();
        Assert.NotNull(button.Template);

        foreach (var partName in requiredTemplateParts)
        {
            var part = button.Template.FindName(partName, button);
            Assert.NotNull(part);
        }

        if (background is not null)
        {
            var swatch = Assert.IsType<Border>(button.Template.FindName("Swatch", button));
            Assert.Same(background, swatch.Background);
        }
    }

    private static void AssertThemedPasswordBoxTemplate()
    {
        var style = Assert.IsType<Style>(Application.Current!.Resources[typeof(PasswordBox)]);
        var passwordBox = new PasswordBox
        {
            Style = style
        };

        passwordBox.ApplyTemplate();
        Assert.NotNull(passwordBox.Template);
        Assert.NotNull(passwordBox.Template.FindName("Bd", passwordBox));
        Assert.NotNull(passwordBox.Template.FindName("PART_ContentHost", passwordBox));
    }

    private static void AssertVerticalGridSplitterTemplate()
    {
        var style = Assert.IsType<Style>(Application.Current!.Resources["VerticalGridSplitter"]);
        var splitter = new GridSplitter
        {
            Style = style
        };

        splitter.ApplyTemplate();

        Assert.Equal(6, splitter.Width);
        Assert.Equal(6, splitter.MinWidth);
        Assert.Equal(Cursors.SizeWE, splitter.Cursor);
        Assert.NotNull(splitter.Template);
        Assert.NotNull(splitter.Template.FindName("HitArea", splitter));
        var hairline = Assert.IsType<Border>(splitter.Template.FindName("Hairline", splitter));
        Assert.Equal(1, hairline.Width);
    }

    private static void AssertThemeContrast(string themeFileName, string foregroundKey, string backgroundKey, double minimumRatio)
    {
        var dictionary = LoadThemeDictionary(themeFileName);
        var foreground = Assert.IsType<SolidColorBrush>(dictionary[foregroundKey]).Color;
        var background = Assert.IsType<SolidColorBrush>(dictionary[backgroundKey]).Color;
        var contrast = ContrastRatio(foreground, background);

        Assert.True(
            contrast >= minimumRatio,
            $"{themeFileName} {foregroundKey} contrast {contrast:0.##}:1 is below {minimumRatio:0.##}:1.");
    }

    private static ResourceDictionary LoadThemeDictionary(string themeFileName)
        => (ResourceDictionary)Application.LoadComponent(
            new Uri($"/Images;component/Themes/{themeFileName}", UriKind.Relative));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(IOPath.Combine(directory.FullName, "Images.sln")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the Images repository root.");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
        => (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));

    private static double Linearize(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static void AssertWindowContract(WindowContractCase testCase)
    {
        var window = testCase.CreateWindow();
        try
        {
            window.Show();
            window.Activate();
            window.UpdateLayout();

            Assert.Contains(testCase.ExpectedTitle, window.Title, StringComparison.OrdinalIgnoreCase);

            var descendants = EnumerateDescendants(window).OfType<FrameworkElement>().ToArray();
            foreach (var automationName in testCase.RequiredAutomationNames)
            {
                var matchingElement = descendants.FirstOrDefault(element =>
                    string.Equals(
                        AutomationProperties.GetName(element),
                        automationName,
                        StringComparison.Ordinal));

                Assert.NotNull(matchingElement);
            }

            var namedFocusableControls = descendants
                .OfType<Control>()
                .Where(control =>
                    control.Focusable &&
                    control.IsEnabled &&
                    control.IsVisible &&
                    !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                .ToArray();
            Assert.NotEmpty(namedFocusableControls);

            Keyboard.Focus(namedFocusableControls[0]);
            Assert.NotNull(Keyboard.FocusedElement);

            var whitespaceHelpTextElement = descendants.FirstOrDefault(element =>
            {
                var helpText = AutomationProperties.GetHelpText(element);
                return helpText.Length > 0 && string.IsNullOrWhiteSpace(helpText);
            });
            Assert.Null(whitespaceHelpTextElement);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertPseudoWindowContract(WindowContractCase testCase)
    {
        var window = testCase.CreateWindow();
        try
        {
            window.Show();
            window.Activate();
            window.UpdateLayout();

            Assert.Contains("[!!", window.Title, StringComparison.Ordinal);
            Assert.Contains(testCase.ExpectedTitle, window.Title, StringComparison.OrdinalIgnoreCase);

            var descendants = EnumerateDescendants(window).OfType<FrameworkElement>().ToArray();
            foreach (var automationName in testCase.RequiredAutomationNames)
            {
                var matchingElement = descendants.FirstOrDefault(element =>
                    string.Equals(
                        AutomationProperties.GetName(element),
                        automationName,
                        StringComparison.Ordinal));

                Assert.NotNull(matchingElement);
                var failures = FindCriticalTextFitFailures(matchingElement!).ToArray();
                Assert.Empty(failures);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<string> FindCriticalTextFitFailures(FrameworkElement root)
    {
        IReadOnlyList<TextBlock> textBlocks = root switch
        {
            TextBlock textBlock => [textBlock],
            ButtonBase => EnumerateDescendants(root).OfType<TextBlock>().ToArray(),
            _ => []
        };

        foreach (var text in textBlocks)
        {
            if (!ShouldMeasureTextBlock(text))
                continue;

            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desiredWidth = text.DesiredSize.Width;
            var desiredHeight = text.DesiredSize.Height;

            var allowedWidth = text.ActualWidth + 2.0;
            var allowedHeight = text.ActualHeight + 2.0;
            if (desiredWidth > allowedWidth || desiredHeight > allowedHeight)
            {
                yield return
                    $"{GetElementDescription(root)} clipped '{text.Text}' " +
                    $"desired={desiredWidth:0.##}x{desiredHeight:0.##} actual={text.ActualWidth:0.##}x{text.ActualHeight:0.##}.";
            }
        }
    }

    private static bool ShouldMeasureTextBlock(TextBlock textBlock)
    {
        if (!textBlock.IsVisible || textBlock.ActualWidth <= 0 || textBlock.ActualHeight <= 0)
            return false;

        if (textBlock.TextWrapping != TextWrapping.NoWrap || textBlock.TextTrimming != TextTrimming.None)
            return false;

        var text = textBlock.Text;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Length > 2 || !text.Any(character => character >= '\uE000');
    }

    private static string GetElementDescription(FrameworkElement element)
    {
        var automationName = AutomationProperties.GetName(element);
        if (!string.IsNullOrWhiteSpace(automationName))
            return automationName;

        return string.IsNullOrWhiteSpace(element.Name)
            ? element.GetType().Name
            : $"{element.GetType().Name}.{element.Name}";
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            yield return current;

            var visualChildCount = 0;
            try
            {
                visualChildCount = VisualTreeHelper.GetChildrenCount(current);
            }
            catch (InvalidOperationException)
            {
            }

            for (var index = visualChildCount - 1; index >= 0; index--)
                stack.Push(VisualTreeHelper.GetChild(current, index));

            foreach (var logicalChild in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
                stack.Push(logicalChild);
        }
    }
}
