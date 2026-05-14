using System.Collections.Specialized;
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

    public static void SetImageAndPath(BitmapSource image, string path)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A path is required.", nameof(path));

        var data = new DataObject();
        data.SetImage(image);
        data.SetText(path);
        data.SetFileDropList(new StringCollection { path });
        Clipboard.SetDataObject(data, copy: true);
    }
}
