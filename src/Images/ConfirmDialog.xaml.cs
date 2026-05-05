using System.Windows;
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
            title: "Move to Recycle Bin?",
            message: "This keeps the file recoverable, but removes it from the current folder and advances the viewer.",
            subject: fileName,
            detail: folder,
            footnote: "You can restore the file from the Recycle Bin until it is emptied.",
            confirmText: "Move to Recycle Bin",
            cancelText: "Cancel",
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
            title: "Clear reference board?",
            message: "This removes the current board layout from this window. Export first if you need to keep a copy.",
            subject: $"{itemCount} board item{(itemCount == 1 ? string.Empty : "s")}",
            detail: "Images and notes are removed from the canvas only. Source files are not changed.",
            footnote: "This action cannot be undone.",
            confirmText: "Clear board",
            cancelText: "Cancel")
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
