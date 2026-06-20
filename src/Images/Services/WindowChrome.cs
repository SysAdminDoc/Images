using System.Runtime.InteropServices;

namespace Images.Services;

/// <summary>
/// Windows 10/11 caption-bar theming via DWM. Best-effort — every P/Invoke ignores failures so an
/// older OS or a missing DLL gracefully falls back to default chrome with no visual regression.
/// </summary>
internal static class WindowChrome
{
    // From dwmapi.h. Immersive-dark-mode is stable from Windows 10 20H1 (build 19041).
    // DWMWA_USE_IMMERSIVE_DARK_MODE_OLD was 19 during preview; 20 is the stable name shipped builds honor.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    private const int MochaBaseColorRef = 0x002E1E1E;
    private const int MochaSurfaceColorRef = 0x00443231;
    private const int MochaTextColorRef = 0x00F4D6CD;

    private const int LatteBaseColorRef = 0x00EFE9E6;
    private const int LatteSurfaceColorRef = 0x00DAD0CC;
    private const int LatteTextColorRef = 0x00694F4C;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyDarkCaption(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        if (ThemeService.CurrentMode == AppThemeMode.Latte)
        {
            ApplyLightCaption(hwnd);
            return;
        }

        int value = 1;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)); }
        catch { /* pre-20H1 silently no-ops */ }

        SetAttribute(hwnd, DWMWA_CAPTION_COLOR, MochaBaseColorRef);
        SetAttribute(hwnd, DWMWA_TEXT_COLOR, MochaTextColorRef);
        SetAttribute(hwnd, DWMWA_BORDER_COLOR, MochaSurfaceColorRef);
    }

    private static void ApplyLightCaption(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        int value = 0;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)); }
        catch { /* pre-20H1 silently no-ops */ }

        SetAttribute(hwnd, DWMWA_CAPTION_COLOR, LatteBaseColorRef);
        SetAttribute(hwnd, DWMWA_TEXT_COLOR, LatteTextColorRef);
        SetAttribute(hwnd, DWMWA_BORDER_COLOR, LatteSurfaceColorRef);
    }

    private static void SetAttribute(IntPtr hwnd, int attribute, int value)
    {
        try { DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)); }
        catch { /* unsupported DWM attribute */ }
    }
}
