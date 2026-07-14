using System.Windows;
using Images.Localization;
using Images.Services;
using Images.ViewModels;
using Microsoft.Win32;

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

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var dialog = new SaveFileDialog
        {
            Title = Strings.SettingsTransferExportDialogTitle,
            Filter = $"{Strings.SettingsTransferJsonFilter}|*.json|{Strings.SettingsTransferAllFilesFilter}|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = "images-settings.json"
        };

        if (dialog.ShowDialog(this) == true)
            viewModel.ExportPortableSettings(dialog.FileName);
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var dialog = new OpenFileDialog
        {
            Title = Strings.SettingsTransferImportDialogTitle,
            Filter = $"{Strings.SettingsTransferJsonFilter}|*.json|{Strings.SettingsTransferAllFilesFilter}|*.*",
            DefaultExt = ".json",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var preview = viewModel.PreviewPortableSettingsImport(dialog.FileName);
        if (preview is null)
            return;

        var confirmed = MessageBox.Show(
            this,
            SettingsViewModel.BuildPortableSettingsImportPreview(preview),
            Strings.SettingsTransferPreviewTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.No);
        if (confirmed == MessageBoxResult.Yes)
            viewModel.ApplyPortableSettingsImport(preview);
    }
}
