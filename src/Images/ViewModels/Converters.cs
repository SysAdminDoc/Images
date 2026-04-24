using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace Images.ViewModels;

/// <summary>
/// Overrides the built-in BooleanToVisibilityConverter with an "Inverse" parameter.
/// Registered in Themes/DarkTheme.xaml under the key "BoolToVis" so every view
/// can reference it as {StaticResource BoolToVis} without a per-view declaration.
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public static readonly BooleanToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value switch
        {
            bool x => x,
            int n => n != 0,
            string s => !string.IsNullOrEmpty(s),
            null => false,
            _ => true
        };
        var inverse = parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        return (b ^ inverse) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class PathToFileNameConverter : IValueConverter
{
    public static readonly PathToFileNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Path.GetFileName(s) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
