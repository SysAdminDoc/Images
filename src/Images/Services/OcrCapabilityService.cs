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
                    "Text extraction ready",
                    "Images uses the installed Windows OCR languages on this PC. No image data is sent to a cloud service.",
                    "Ready")
                : new OcrCapabilityStatus(
                    false,
                    0,
                    "No Windows OCR languages installed",
                    "OCR needs setup",
                    "Install a Windows language pack with OCR support, then refresh this status.",
                    "Setup needed");
        }
        catch (Exception ex)
        {
            return new OcrCapabilityStatus(
                false,
                0,
                "Windows OCR status could not be checked",
                "OCR status unavailable",
                $"Windows OCR could not be queried: {ex.Message}",
                "Check failed");
        }
    }

    public static string BuildOverviewText()
    {
        var status = GetStatus();
        return status.IsAvailable
            ? $"Ready — {status.LanguageSummary}"
            : status.LanguageSummary;
    }

    public static string BuildSystemInfoText()
    {
        var status = GetStatus();
        return status.IsAvailable
            ? $"available ({status.LanguageSummary})"
            : status.LanguageSummary;
    }

    private static string FormatLanguageSummary(IReadOnlyList<Language> languages)
    {
        if (languages.Count == 0)
            return "No Windows OCR languages installed";

        var displayNames = languages
            .Select(language => string.IsNullOrWhiteSpace(language.DisplayName)
                ? language.LanguageTag
                : $"{language.DisplayName} ({language.LanguageTag})")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var suffix = languages.Count > displayNames.Count
            ? $" + {languages.Count - displayNames.Count} more"
            : "";

        return string.Join(", ", displayNames) + suffix;
    }
}
