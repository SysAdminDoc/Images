using System.Windows;
using Microsoft.Win32;

namespace Images.Services;

public static class ThemeService
{
    internal const string ThemeMarkerKey = "Images.Theme.Name";
    internal const string HighContrastThemeName = "HighContrast";

    private static readonly Uri HighContrastThemeUri = new("Themes/HighContrastTheme.xaml", UriKind.Relative);
    private static readonly object Gate = new();

    private static Application? _application;
    private static SettingsService? _settings;
    private static bool _systemEventsSubscribed;

    public static void Initialize(Application application, SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(settings);

        lock (Gate)
        {
            if (_application is not null)
                _application.Exit -= OnApplicationExit;

            _application = application;
            _settings = settings;
            _application.Exit += OnApplicationExit;
        }

        ApplyFromSettings(settings);
        SubscribeSystemEvents();
    }

    public static void ApplyFromSettings(SettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var application = Application.Current;
        if (application is null) return;

        var highContrast = ShouldUseHighContrast(
            settings.GetBool(Keys.AccessibilityHighContrast, false),
            SystemParameters.HighContrast);

        SetHighContrastDictionary(application.Resources, highContrast);
    }

    internal static bool ShouldUseHighContrast(bool appPreference, bool systemHighContrast)
        => appPreference || systemHighContrast;

    internal static void SetHighContrastDictionary(
        ResourceDictionary resources,
        bool enabled,
        bool refresh = false,
        Func<ResourceDictionary>? createDictionary = null)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var existing = FindHighContrastDictionary(resources);
        if (existing is not null && (!enabled || refresh))
        {
            resources.MergedDictionaries.Remove(existing);
            existing = null;
        }

        if (!enabled || existing is not null) return;

        resources.MergedDictionaries.Add((createDictionary ?? CreateHighContrastDictionary)());
    }

    internal static bool IsHighContrastDictionary(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (dictionary.Source is not null &&
            dictionary.Source.OriginalString.EndsWith("HighContrastTheme.xaml", StringComparison.OrdinalIgnoreCase))
            return true;

        return dictionary.Contains(ThemeMarkerKey) &&
               string.Equals(dictionary[ThemeMarkerKey] as string, HighContrastThemeName, StringComparison.Ordinal);
    }

    private static ResourceDictionary? FindHighContrastDictionary(ResourceDictionary resources)
    {
        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (IsHighContrastDictionary(dictionary))
                return dictionary;
        }

        return null;
    }

    private static ResourceDictionary CreateHighContrastDictionary()
        => new() { Source = HighContrastThemeUri };

    private static void SubscribeSystemEvents()
    {
        lock (Gate)
        {
            if (_systemEventsSubscribed) return;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _systemEventsSubscribed = true;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var application = Application.Current;
        var settings = _settings;
        if (application is null || settings is null) return;

        void Apply()
        {
            var highContrast = ShouldUseHighContrast(
                settings.GetBool(Keys.AccessibilityHighContrast, false),
                SystemParameters.HighContrast);

            SetHighContrastDictionary(application.Resources, highContrast, refresh: highContrast);
        }

        if (application.Dispatcher.CheckAccess())
            Apply();
        else
            application.Dispatcher.BeginInvoke(Apply);
    }

    private static void OnApplicationExit(object sender, ExitEventArgs e) => Shutdown();

    private static void Shutdown()
    {
        lock (Gate)
        {
            if (_systemEventsSubscribed)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                _systemEventsSubscribed = false;
            }

            if (_application is not null)
            {
                _application.Exit -= OnApplicationExit;
                _application = null;
            }

            _settings = null;
        }
    }
}
