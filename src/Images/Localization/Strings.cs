using System.Globalization;
using System.Resources;

namespace Images.Localization;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("Images.Localization.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string Cancel => Get(nameof(Cancel));
    public static string ConfirmAction => Get(nameof(ConfirmAction));
    public static string ConfirmDestructiveAction => Get(nameof(ConfirmDestructiveAction));
    public static string ConfirmRecycleBinButton => Get(nameof(ConfirmRecycleBinButton));
    public static string ConfirmRecycleBinDontAskAgain => Get(nameof(ConfirmRecycleBinDontAskAgain));
    public static string ConfirmRecycleBinDontAskAgainAutomationName => Get(nameof(ConfirmRecycleBinDontAskAgainAutomationName));
    public static string ConfirmRecycleBinDontAskAgainHelpText => Get(nameof(ConfirmRecycleBinDontAskAgainHelpText));
    public static string ConfirmRecycleBinFootnote => Get(nameof(ConfirmRecycleBinFootnote));
    public static string ConfirmRecycleBinMessage => Get(nameof(ConfirmRecycleBinMessage));
    public static string ConfirmRecycleBinTitle => Get(nameof(ConfirmRecycleBinTitle));
    public static string ConfirmReferenceBoardButton => Get(nameof(ConfirmReferenceBoardButton));
    public static string ConfirmReferenceBoardClearTitle => Get(nameof(ConfirmReferenceBoardClearTitle));
    public static string ConfirmReferenceBoardDetail => Get(nameof(ConfirmReferenceBoardDetail));
    public static string ConfirmReferenceBoardFootnote => Get(nameof(ConfirmReferenceBoardFootnote));
    public static string ConfirmReferenceBoardMessage => Get(nameof(ConfirmReferenceBoardMessage));
    public static string Close => Get(nameof(Close));
    public static string ClipboardBusyRetry => Get(nameof(ClipboardBusyRetry));
    public static string CrashDetailsCopied => Get(nameof(CrashDetailsCopied));
    public static string CrashDialogTitle => Get(nameof(CrashDialogTitle));
    public static string CrashIssueBrowserAndCopyFailed => Get(nameof(CrashIssueBrowserAndCopyFailed));
    public static string CrashIssueBrowserFailedCopied => Get(nameof(CrashIssueBrowserFailedCopied));
    public static string CrashIssueOpened => Get(nameof(CrashIssueOpened));
    public static string CrashLogFolderOpened => Get(nameof(CrashLogFolderOpened));
    public static string InnerExceptionSeparator => Get(nameof(InnerExceptionSeparator));
    public static string LogFolderUnavailable => Get(nameof(LogFolderUnavailable));
    public static string ResizeEdge => Get(nameof(ResizeEdge));
    public static string ResizeFilterBicubicDescription => Get(nameof(ResizeFilterBicubicDescription));
    public static string ResizeFilterBicubicLabel => Get(nameof(ResizeFilterBicubicLabel));
    public static string ResizeFilterLanczos3Description => Get(nameof(ResizeFilterLanczos3Description));
    public static string ResizeFilterLanczos3Label => Get(nameof(ResizeFilterLanczos3Label));
    public static string ResizeFilterMitchellDescription => Get(nameof(ResizeFilterMitchellDescription));
    public static string ResizeFilterMitchellLabel => Get(nameof(ResizeFilterMitchellLabel));
    public static string ResizeLongEdge => Get(nameof(ResizeLongEdge));
    public static string ResizeModeLongEdgeDescription => Get(nameof(ResizeModeLongEdgeDescription));
    public static string ResizeModePercentDescription => Get(nameof(ResizeModePercentDescription));
    public static string ResizeModePixelsDescription => Get(nameof(ResizeModePixelsDescription));
    public static string ResizeModeShortEdgeDescription => Get(nameof(ResizeModeShortEdgeDescription));
    public static string ResizeOpenValidImageError => Get(nameof(ResizeOpenValidImageError));
    public static string ResizeOutputMinimumError => Get(nameof(ResizeOutputMinimumError));
    public static string ResizePercent => Get(nameof(ResizePercent));
    public static string ResizePixelSize => Get(nameof(ResizePixelSize));
    public static string ResizePlanChooseFilterError => Get(nameof(ResizePlanChooseFilterError));
    public static string ResizePlanEnterPositiveValuesError => Get(nameof(ResizePlanEnterPositiveValuesError));
    public static string ResizePlanLabel => Get(nameof(ResizePlanLabel));
    public static string ResizeReadyStatus => Get(nameof(ResizeReadyStatus));
    public static string ResizeShortEdge => Get(nameof(ResizeShortEdge));

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return ResourceManager.GetString(key, Culture) ?? $"!{key}!";
    }

    public static string Format(string key, params object?[] args)
        => string.Format(Culture ?? CultureInfo.CurrentUICulture, Get(key), args);
}
