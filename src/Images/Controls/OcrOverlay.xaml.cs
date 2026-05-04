using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.ViewModels;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.Controls;

public partial class OcrOverlay : UserControl
{
    private static readonly ILogger _log = Log.For<OcrOverlay>();

    public OcrOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Find the parent ZoomPanImage control by walking up the visual tree
        var parent = VisualTreeHelper.GetParent(this);
        while (parent != null)
        {
            if (parent is Grid grid && VisualTreeHelper.GetChildrenCount(grid) > 0)
            {
                // Check if first child is ZoomPanImage
                var firstChild = VisualTreeHelper.GetChild(grid, 0);
                if (firstChild?.GetType().Name == "ZoomPanImage")
                {
                    // Found it — get its CurrentZoom property via reflection
                    var zoomProperty = firstChild.GetType().GetProperty("CurrentZoom");
                    if (zoomProperty != null)
                    {
                        // Create binding to zoom and update transform
                        var bindingTimer = new System.Windows.Threading.DispatcherTimer();
                        bindingTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
                        bindingTimer.Tick += (_, _) =>
                        {
                            if (zoomProperty.GetValue(firstChild) is double zoom && zoom > 0)
                            {
                                Root.RenderTransform = new ScaleTransform(zoom, zoom);
                            }
                        };
                        bindingTimer.Start();
                    }
                    break;
                }
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
    }

    private void TextRegion_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not OcrTextLine line) return;

        // Copy text to clipboard
        try
        {
            Clipboard.SetText(line.Text);
            
            // Show visual feedback
            line.IsSelected = true;
            
            // Reset selection after brief delay
            System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => line.IsSelected = false);
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clipboard copy failed: {Message}", ex.Message);
        }

        e.Handled = true;
    }
}
