using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Images.Localization;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class SecondaryWindowXamlTests
{
    private sealed record WindowContractCase(
        string ExpectedTitle,
        Func<Window> CreateWindow,
        string[] RequiredAutomationNames);

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

                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                foreach (var testCase in CreateWindowContractCases())
                    AssertWindowContract(testCase);

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

                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                foreach (var testCase in CreateWindowContractCases())
                    AssertPseudoWindowContract(testCase);

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
            Strings.ImportInboxWindowTitle,
            static () => new Images.ImportInboxWindow(),
            [
                Strings.ImportInboxAddFiles,
                Strings.ImportInboxAddFolder,
                Strings.ImportInboxImportPicasa,
                Strings.ImportInboxChoose,
                Strings.ImportInboxStagedFiles
            ])
    ];

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
