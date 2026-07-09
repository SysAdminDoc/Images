using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.Localization;
using Images.Services;

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

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("#F9E2AF", true)]
    [InlineData("#000000", false)]
    public void ContrastingTextBrushFor_SelectsReadableNumberLabelBrush(string fill, bool expectBlack)
    {
        var brush = AnnotationsWindow.ContrastingTextBrushFor(fill);

        Assert.Same(expectBlack ? Brushes.Black : Brushes.White, brush);
    }

    [Fact]
    public void LostMouseCapture_ClearsTransientDragState()
    {
        RunOnStaWithTheme(() =>
        {
            using var temp = TestDirectory.Create();
            var missing = Path.Combine(temp.Path, "missing.png");
            var window = new AnnotationsWindow(missing, _ => throw new InvalidOperationException("Should not apply."));
            try
            {
                SetField(window, "_isDragging", true);
                SetField(window, "_dragStart", new Point(1, 1));
                var points = Assert.IsType<List<ImageAnnotationPoint>>(GetField(window, "_freehandPoints"));
                points.Add(new ImageAnnotationPoint(1, 1));

                InvokePrivate(window, "PreviewCanvas_LostMouseCapture", window.FindName("PreviewCanvas"), null);

                Assert.False(Assert.IsType<bool>(GetField(window, "_isDragging")));
                Assert.Null(GetField(window, "_dragStart"));
                Assert.Empty(points);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void SetField(AnnotationsWindow window, string name, object? value)
        => GetFieldInfo(name).SetValue(window, value);

    private static object? GetField(AnnotationsWindow window, string name)
        => GetFieldInfo(name).GetValue(window);

    private static FieldInfo GetFieldInfo(string name)
        => typeof(AnnotationsWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException($"Field not found: {name}");

    private static void InvokePrivate(AnnotationsWindow window, string name, params object?[] args)
        => (typeof(AnnotationsWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method not found: {name}"))
            .Invoke(window, args);

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
