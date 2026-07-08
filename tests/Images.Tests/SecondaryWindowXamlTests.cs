using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    public void ThemedButtonStylesReplaceDefaultChrome()
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
