using System.IO;
using System.Windows;

namespace Images.Services;

public sealed class RecycleBinDeleteService
{
    private readonly SettingsService _settings;
    private readonly Action<string> _sendToRecycleBin;
    private readonly Func<Window?, string, ConfirmDialog.ConfirmationResult> _confirmRecycleBinMove;

    public RecycleBinDeleteService(
        SettingsService settings,
        Action<string>? sendToRecycleBin = null,
        Func<Window?, string, ConfirmDialog.ConfirmationResult>? confirmRecycleBinMove = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _sendToRecycleBin = sendToRecycleBin ?? SendToRecycleBin;
        _confirmRecycleBinMove = confirmRecycleBinMove ?? ConfirmDialog.ConfirmRecycleBinMove;
    }

    public RecycleBinDeleteResult Delete(string path, Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return RecycleBinDeleteResult.Missing();

        if (_settings.GetBool(Keys.ConfirmRecycleBinDelete, true))
        {
            var confirmation = _confirmRecycleBinMove(owner, path);
            if (!confirmation.Confirmed)
                return RecycleBinDeleteResult.Canceled();

            if (confirmation.DoNotAskAgain)
                _settings.SetBool(Keys.ConfirmRecycleBinDelete, false);
        }

        try
        {
            _sendToRecycleBin(path);
            return RecycleBinDeleteResult.Deleted(Path.GetFileName(path));
        }
        catch (OperationCanceledException)
        {
            return RecycleBinDeleteResult.Canceled();
        }
        catch (Exception ex)
        {
            return RecycleBinDeleteResult.Failed(ex.Message);
        }
    }

    private static void SendToRecycleBin(string path)
    {
        // ThrowException (not DoNothing): when the shell surfaces an error
        // dialog (file locked, slow share) and the user cancels there,
        // DeleteFile returns normally without deleting — reporting Deleted
        // would remove a still-existing file from the navigator and log a
        // phantom recovery record. The existence re-check covers any silent
        // no-op path the shell doesn't signal.
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
            Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);

        if (File.Exists(path))
            throw new OperationCanceledException("The file was not moved to the Recycle Bin.");
    }
}

public enum RecycleBinDeleteStatus
{
    Deleted,
    Canceled,
    Missing,
    Failed
}

public readonly record struct RecycleBinDeleteResult(
    RecycleBinDeleteStatus Status,
    string? FileName = null,
    string? ErrorMessage = null)
{
    public static RecycleBinDeleteResult Deleted(string fileName) =>
        new(RecycleBinDeleteStatus.Deleted, fileName);

    public static RecycleBinDeleteResult Canceled() =>
        new(RecycleBinDeleteStatus.Canceled);

    public static RecycleBinDeleteResult Missing() =>
        new(RecycleBinDeleteStatus.Missing);

    public static RecycleBinDeleteResult Failed(string errorMessage) =>
        new(RecycleBinDeleteStatus.Failed, ErrorMessage: errorMessage);
}
