using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Images.Localization;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class AnnotationsWindowTests
{
    [Fact]
    public void Constructor_PreservesMissingFileStatus()
    {
        RunOnStaWithTheme(() =>
        {
            using var temp = TestDirectory.Create();
            var missing = Path.Combine(temp.Path, "missing.png");
            var window = new AnnotationsWindow(missing, _ => throw new InvalidOperationException("Should not apply."));
            try
            {
                var status = Assert.IsType<TextBlock>(window.FindName("StatusText"));
                var applyButton = Assert.IsType<Button>(window.FindName("ApplyButton"));

                Assert.Equal(Strings.AnnotationUnavailableMissingFile, status.Text);
                Assert.False(applyButton.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ShouldHandleWindowKey_IgnoresTextEntryFocus()
    {
        RunOnStaWithTheme(() =>
        {
            Assert.False(AnnotationsWindow.ShouldHandleWindowKey(Key.Enter, new TextBox()));
            Assert.False(AnnotationsWindow.ShouldHandleWindowKey(Key.Escape, new RichTextBox()));
            Assert.False(AnnotationsWindow.ShouldHandleWindowKey(Key.A, new Button()));
            Assert.True(AnnotationsWindow.ShouldHandleWindowKey(Key.Enter, new Button()));
            Assert.True(AnnotationsWindow.ShouldHandleWindowKey(Key.Escape, null));
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
