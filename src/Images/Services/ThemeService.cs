using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Images.Services;

public enum AppThemeMode
{
    Dark,
    Latte,
    HighContrast,
    System,
}

public static class ThemeService
{
    internal const string ThemeMarkerKey = "Images.Theme.Name";
    internal const string HighContrastThemeName = "HighContrast";
    internal const string LatteName = "Latte";

    private static readonly Uri HighContrastThemeUri = new("pack://application:,,,/Images;component/Themes/HighContrastTheme.xaml", UriKind.Absolute);
    private static readonly Uri LatteThemeUri = new("pack://application:,,,/Images;component/Themes/LatteTheme.xaml", UriKind.Absolute);
    private static readonly object Gate = new();

    private static Application? _application;
    private static SettingsService? _settings;
    private static bool _systemEventsSubscribed;

    public static AppThemeMode CurrentMode { get; private set; } = AppThemeMode.Dark;

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

        var mode = ParseThemeMode(settings.GetString(Keys.ThemeMode, "dark"));
        var resolvedMode = ResolveMode(mode, SystemParameters.HighContrast, IsSystemLightTheme());

        var highContrast = ShouldUseHighContrast(
            settings.GetBool(Keys.AccessibilityHighContrast, false),
            SystemParameters.HighContrast) || resolvedMode == AppThemeMode.HighContrast;
        var effectiveMode = ResolveEffectiveMode(resolvedMode, highContrast);

        // refresh: true recreates the dictionary when HC is already active —
        // its tokens are one-time SystemColors snapshots, so switching between
        // Windows HC schemes (Black/White) would otherwise keep stale colors
        // until restart.
        SetHighContrastDictionary(application.Resources, highContrast, refresh: highContrast);
        SetLatteOverlay(application.Resources, effectiveMode == AppThemeMode.Latte);
        CurrentMode = effectiveMode;
        ReapplyCaptionsToOpenWindows(application);
    }

    private static void ReapplyCaptionsToOpenWindows(Application application)
    {
        RunOnDispatcher(application.Dispatcher, () =>
        {
            foreach (Window window in application.Windows)
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                    WindowChrome.ApplyDarkCaption(hwnd);
            }
        });
    }

    internal static void RunOnDispatcher(Dispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return;

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(action);
        }
        catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            // The owning window closed between the shutdown check and queueing.
        }
    }

    public static void SetTheme(AppThemeMode mode)
    {
        var settings = _settings;
        if (settings is null) return;

        var modeString = mode switch
        {
            AppThemeMode.Latte => "latte",
            AppThemeMode.HighContrast => "high-contrast",
            AppThemeMode.System => "system",
            _ => "dark",
        };

        settings.SetString(Keys.ThemeMode, modeString);
        ApplyFromSettings(settings);
    }

    internal static AppThemeMode ParseThemeMode(string? value) => value switch
    {
        "latte" => AppThemeMode.Latte,
        "high-contrast" => AppThemeMode.HighContrast,
        "system" => AppThemeMode.System,
        _ => AppThemeMode.Dark,
    };

    internal static AppThemeMode ResolveMode(AppThemeMode requested, bool systemHighContrast, bool systemLight)
    {
        if (systemHighContrast) return AppThemeMode.HighContrast;
        if (requested == AppThemeMode.System) return systemLight ? AppThemeMode.Latte : AppThemeMode.Dark;
        return requested;
    }

    internal static AppThemeMode ResolveEffectiveMode(AppThemeMode resolvedMode, bool highContrast)
        => highContrast ? AppThemeMode.HighContrast : resolvedMode;

    internal static bool ShouldUseHighContrast(bool appPreference, bool systemHighContrast)
        => appPreference || systemHighContrast;

    internal static void SetHighContrastDictionary(
        ResourceDictionary resources,
        bool enabled,
        bool refresh = false,
        Func<ResourceDictionary>? createDictionary = null)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var existing = FindDictionary(resources, IsHighContrastDictionary);
        if (existing is not null && (!enabled || refresh))
        {
            resources.MergedDictionaries.Remove(existing);
            existing = null;
        }

        if (!enabled || existing is not null) return;

        resources.MergedDictionaries.Add((createDictionary ?? CreateHighContrastDictionary)());
    }

    internal static void SetLatteOverlay(ResourceDictionary resources, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var existing = FindDictionary(resources, IsLatteDictionary);
        if (existing is not null && !enabled)
        {
            resources.MergedDictionaries.Remove(existing);
            return;
        }

        if (!enabled || existing is not null) return;
        resources.MergedDictionaries.Add(new ResourceDictionary { Source = LatteThemeUri });
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

    internal static bool IsLatteDictionary(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (dictionary.Source is not null &&
            dictionary.Source.OriginalString.EndsWith("LatteTheme.xaml", StringComparison.OrdinalIgnoreCase))
            return true;

        return dictionary.Contains(ThemeMarkerKey) &&
               string.Equals(dictionary[ThemeMarkerKey] as string, LatteName, StringComparison.Ordinal);
    }

    private static ResourceDictionary? FindDictionary(ResourceDictionary resources, Func<ResourceDictionary, bool> predicate)
    {
        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (predicate(dictionary))
                return dictionary;
        }

        return null;
    }

    private static ResourceDictionary CreateHighContrastDictionary()
        => new() { Source = HighContrastThemeUri };

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intVal && intVal == 1;
        }
        catch
        {
            return false;
        }
    }

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

        void Apply() => ApplyFromSettings(settings);

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
