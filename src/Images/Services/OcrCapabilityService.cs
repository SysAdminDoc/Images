using Images.Localization;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Images.Services;

public static class OcrCapabilityService
{
    public sealed record OcrCapabilityStatus(
        bool IsAvailable,
        int LanguageCount,
        string LanguageSummary,
        string StatusTitle,
        string StatusDetail,
        string BadgeText);

    public static OcrCapabilityStatus GetStatus()
    {
        try
        {
            var languages = OcrEngine.AvailableRecognizerLanguages.ToList();
            var summary = FormatLanguageSummary(languages);

            return languages.Count > 0
                ? new OcrCapabilityStatus(
                    true,
                    languages.Count,
                    summary,
                    Strings.OcrCapabilityReadyTitle,
                    Strings.OcrCapabilityReadyDetail,
                    Strings.OcrCapabilityReadyBadge)
                : new OcrCapabilityStatus(
                    false,
                    0,
                    Strings.OcrCapabilityNoLanguages,
                    Strings.OcrCapabilitySetupTitle,
                    Strings.OcrCapabilitySetupDetail,
                    Strings.OcrCapabilitySetupBadge);
        }
        catch (Exception ex)
        {
            return new OcrCapabilityStatus(
                false,
                0,
                Strings.OcrCapabilityCheckUnavailable,
                Strings.OcrCapabilityUnavailableTitle,
                Strings.Format(nameof(Strings.OcrCapabilityQueryFailedFormat), ex.Message),
                Strings.OcrCapabilityCheckFailedBadge);
        }
    }

    public static string BuildOverviewText()
        => BuildOverviewText(GetStatus());

    internal static string BuildOverviewText(OcrCapabilityStatus status)
        => status.IsAvailable
            ? Strings.Format(nameof(Strings.OcrCapabilityOverviewReadyFormat), status.LanguageSummary)
            : status.LanguageSummary;

    public static string BuildSystemInfoText()
        => BuildSystemInfoText(GetStatus());

    internal static string BuildSystemInfoText(OcrCapabilityStatus status)
        => status.IsAvailable
            ? Strings.Format(nameof(Strings.OcrCapabilitySystemInfoAvailableFormat), status.LanguageSummary)
            : status.LanguageSummary;

    private static string FormatLanguageSummary(IReadOnlyList<Language> languages)
    {
        if (languages.Count == 0)
            return Strings.OcrCapabilityNoLanguages;

        var displayNames = languages
            .Select(language => string.IsNullOrWhiteSpace(language.DisplayName)
                ? language.LanguageTag
                : $"{language.DisplayName} ({language.LanguageTag})")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var suffix = languages.Count > displayNames.Count
            ? Strings.Format(nameof(Strings.OcrCapabilityMoreLanguagesFormat), languages.Count - displayNames.Count)
            : "";

        return string.Join(", ", displayNames) + suffix;
    }
}
