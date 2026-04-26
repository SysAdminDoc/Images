using System.Windows;
using Images.Services;
using Images.ViewModels;

namespace Images;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        DataContext = new SettingsViewModel();
        InitializeComponent();

        // Dark native caption — same pattern as MainWindow and AboutWindow.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
