using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Controls;
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

    [Fact]
    public void PremiumShell_UsesCompactImageFirstHierarchy()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();
            try
            {
                Assert.Equal(52, Assert.IsType<Border>(window.FindName("WorkflowRail")).Width);
                Assert.Equal(300, Assert.IsType<Border>(window.FindName("RightSidePanel")).Width);
                Assert.Equal(94, Assert.IsType<ListBox>(window.FindName("FilmstripItems")).MaxHeight);

                var fitButton = Assert.IsType<Button>(window.FindName("FitButton"));
                Assert.Equal("Fit", fitButton.Content);
                Assert.Equal(48, fitButton.Width);

                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("EmptyCapabilityGrid")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("CurrentFileInfoRow")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Expander>(window.FindName("InspectorToolsExpander")).Visibility);
                Assert.False(Assert.IsType<Expander>(window.FindName("MoreDetailsExpander")).IsExpanded);

                var gallery = Assert.IsType<Border>(window.FindName("GalleryWorkbench"));
                Assert.Equal(new Thickness(24, 20, 24, 20), gallery.Padding);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Gallery_RealizesOnlyViewportRows()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();
            try
            {
                var list = Assert.IsType<ListBox>(window.FindName("GalleryItems"));
                list.ItemsSource = Enumerable.Range(0, 1000).ToArray();
                list.Width = 650;
                list.Height = 320;
                list.ApplyTemplate();
                list.Measure(new Size(650, 320));
                list.Arrange(new Rect(0, 0, 650, 320));
                list.UpdateLayout();

                var panel = FindVisualDescendant<VirtualizingWrapPanel>(list);
                Assert.NotNull(panel);
                Assert.InRange(panel.RealizedItemCount, 1, 50);
                Assert.True(panel.ExtentHeight > panel.ViewportHeight);

                panel.SetVerticalOffset(panel.ExtentHeight / 2);
                list.UpdateLayout();
                Assert.True(panel.VerticalOffset > 0);
                Assert.InRange(panel.RealizedItemCount, 1, 50);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindVisualDescendant<T>(child) is { } nested) return nested;
        }

        return null;
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
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
