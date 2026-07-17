using System.Threading;
using System.Windows;
using Images.Services;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class SemanticSearchWindowTests
{
    [Fact]
    public void TagGraphCategorySets_SaveSwapAndDeleteWithoutWritingAnImage()
    {
        using var temp = TestDirectory.Create();
        var sets = new KeywordSetService(System.IO.Path.Combine(temp.Path, "sets.json"));
        var graph = new TagGraphService(System.IO.Path.Combine(temp.Path, "graph.json"));

        RunOnStaWithTheme(() =>
        {
            var window = new TagGraphWindow(graph, sets);
            try
            {
                Assert.True(window.SaveCategorySet("Editorial", "draft\nneeds-review"));
                window.TagInputBox.Clear();

                Assert.True(window.UseSelectedCategorySet());
                Assert.Contains("draft", window.TagInputBox.Text);
                Assert.Contains("needs-review", window.TagInputBox.Text);
                Assert.True(window.DeleteSelectedCategorySet());
                Assert.Empty(sets.Sets);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RevealResult_UsesExplorerRevealAction()
    {
        using var temp = TestDirectory.Create();
        var imagePath = temp.WriteFile("photo.jpg", "not image bytes");
        string? revealedPath = null;

        RunOnStaWithTheme(() =>
        {
            var window = new SemanticSearchWindow(null, path => revealedPath = path);
            try
            {
                window.RevealResult(new SemanticSearchResultRow(
                    imagePath,
                    "photo.jpg",
                    temp.Path,
                    "100.0%",
                    "photo"));

                Assert.Equal(imagePath, revealedPath);
                Assert.Contains("Revealed", window.StatusText.Text, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RevealResult_MissingFileDoesNotOpenExplorer()
    {
        var missingPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.jpg");
        var called = false;

        RunOnStaWithTheme(() =>
        {
            var window = new SemanticSearchWindow(null, _ => called = true);
            try
            {
                window.RevealResult(new SemanticSearchResultRow(
                    missingPath,
                    "missing.jpg",
                    System.IO.Path.GetDirectoryName(missingPath) ?? string.Empty,
                    "100.0%",
                    "missing"));

                Assert.False(called);
                Assert.Equal(Images.Localization.Strings.SemanticSearchResultNoLongerExists, window.StatusText.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaWithTheme(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var createdApplication = Application.Current is null;
            var application = Application.Current ?? new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            try
            {
                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (createdApplication)
                    application.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
