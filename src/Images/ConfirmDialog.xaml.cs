using System.Windows;
using Images.Localization;
using Images.Services;

namespace Images;

public partial class ConfirmDialog : Window
{
    public readonly record struct ConfirmationResult(bool Confirmed, bool DoNotAskAgain);

    private ConfirmDialog(
        string title,
        string message,
        string subject,
        string detail,
        string footnote,
        string confirmText,
        string cancelText,
        bool showDoNotAskAgain = false)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        SubjectText.Text = subject;
        DetailText.Text = detail;
        FootnoteText.Text = footnote;
        ConfirmButtonText.Text = confirmText;
        CancelButtonText.Text = cancelText;
        DontAskAgainPanel.Visibility = showDoNotAskAgain ? Visibility.Visible : Visibility.Collapsed;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    public static ConfirmationResult ConfirmRecycleBinMove(Window? owner, string path)
    {
        var fileName = System.IO.Path.GetFileName(path);
        var folder = System.IO.Path.GetDirectoryName(path) ?? "";

        var dialog = new ConfirmDialog(
            title: Strings.ConfirmRecycleBinTitle,
            message: Strings.ConfirmRecycleBinMessage,
            subject: fileName,
            detail: folder,
            footnote: Strings.ConfirmRecycleBinFootnote,
            confirmText: Strings.ConfirmRecycleBinButton,
            cancelText: Strings.Cancel,
            showDoNotAskAgain: true)
        {
            Owner = owner
        };

        var confirmed = dialog.ShowDialog() == true;
        return new ConfirmationResult(
            confirmed,
            confirmed && dialog.DontAskAgainCheckBox.IsChecked == true);
    }

    public static bool ConfirmReferenceBoardClear(Window? owner, int itemCount)
    {
        var dialog = new ConfirmDialog(
            title: Strings.ConfirmReferenceBoardClearTitle,
            message: Strings.ConfirmReferenceBoardMessage,
            subject: Strings.Format("ConfirmReferenceBoardSubjectFormat", itemCount, itemCount == 1 ? string.Empty : "s"),
            detail: Strings.ConfirmReferenceBoardDetail,
            footnote: Strings.ConfirmReferenceBoardFootnote,
            confirmText: Strings.ConfirmReferenceBoardButton,
            cancelText: Strings.Cancel)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
