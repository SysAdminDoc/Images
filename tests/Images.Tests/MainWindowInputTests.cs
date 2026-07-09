using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Images.Services;

namespace Images.Tests;

public sealed class MainWindowInputTests
{
    [Fact]
    public void IsTextEntryElement_DetectsEditableControls()
    {
        RunOnSta(() =>
        {
            Assert.True(MainWindow.IsTextEntryElement(new TextBox()));
            Assert.True(MainWindow.IsTextEntryElement(new PasswordBox()));
            Assert.True(MainWindow.IsTextEntryElement(new RichTextBox()));
            Assert.True(MainWindow.IsTextEntryElement(new ComboBox { IsEditable = true }));
            Assert.False(MainWindow.IsTextEntryElement(new ComboBox { IsEditable = false }));
            Assert.False(MainWindow.IsTextEntryElement(new Button()));
            Assert.False(MainWindow.IsTextEntryElement(null));
        });
    }

    [Fact]
    public void ResetCanvasPointerStateAfterCaptureLoss_ClearsTransientState()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();
            try
            {
                SetField(window, "_retouchPainting", true);
                SetField(window, "_redEyeCorrectionPainting", true);
                SetField(window, "_exposureBrushPainting", true);
                SetField(window, "_canvasSelectionStart", new PixelCoordinate(1, 1));
                SetField(window, "_cropSelectionStart", new PixelCoordinate(1, 1));
                SetField(window, "_inspectorSelectionStart", new PixelCoordinate(1, 1));

                window.ResetCanvasPointerStateAfterCaptureLoss();

                Assert.False(GetField<bool>(window, "_retouchPainting"));
                Assert.False(GetField<bool>(window, "_redEyeCorrectionPainting"));
                Assert.False(GetField<bool>(window, "_exposureBrushPainting"));
                Assert.Null(GetField<PixelCoordinate?>(window, "_canvasSelectionStart"));
                Assert.Null(GetField<PixelCoordinate?>(window, "_cropSelectionStart"));
                Assert.Null(GetField<PixelCoordinate?>(window, "_inspectorSelectionStart"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void SetField(MainWindow window, string name, object? value)
        => GetFieldInfo(name).SetValue(window, value);

    private static T? GetField<T>(MainWindow window, string name)
        => (T?)GetFieldInfo(name).GetValue(window);

    private static FieldInfo GetFieldInfo(string name)
        => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException($"Field not found: {name}");

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
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

                action();

                if (createdApplication)
                    application.Shutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
