using System.Windows;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Thin application clipboard boundary. Keeps WPF clipboard calls in one place so retry,
/// diagnostics, or test seams can be added without changing every window.
/// </summary>
public static class ClipboardService
{
    public static void SetText(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        Clipboard.SetText(text);
    }

    public static void SetImage(BitmapSource image)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));

        Clipboard.SetImage(image);
    }
}
