using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.Localization;

namespace Images.Tests;

public sealed class SecondaryWindowXamlTests
{
    private sealed record WindowContractCase(
        string ExpectedTitle,
        Func<Window> CreateWindow,
        string[] RequiredAutomationNames);

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
