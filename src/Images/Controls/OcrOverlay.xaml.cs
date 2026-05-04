using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
