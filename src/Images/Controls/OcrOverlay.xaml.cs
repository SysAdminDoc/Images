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
        CopyRegionText(sender);
        e.Handled = true;
    }

    private void TextRegion_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        CopyRegionText(sender);
        e.Handled = true;
    }

    private void CopyRegionText(object sender)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not OcrTextLine line) return;

        try
        {
            Clipboard.SetText(line.Text);
            line.IsSelected = true;
            _ = ResetSelectionAsync(line);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clipboard copy failed: {Message}", ex.Message);
        }
    }

    private async Task ResetSelectionAsync(OcrTextLine line)
    {
        await Task.Delay(200).ConfigureAwait(true);
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        line.IsSelected = false;
    }
}
