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

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return ResourceManager.GetString(key, Culture) ?? $"!{key}!";
    }

    public static string Format(string key, params object?[] args)
        => string.Format(Culture ?? CultureInfo.CurrentUICulture, Get(key), args);
}
