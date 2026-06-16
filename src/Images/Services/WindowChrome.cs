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

    // DWMWA_SYSTEMBACKDROP_TYPE landed with Windows 11 22H2 (build 22621). Values:
    //   1 = DWMSBT_DISABLE, 2 = DWMSBT_MAINWINDOW (Mica), 3 = DWMSBT_TRANSIENTWINDOW (Acrylic), 4 = DWMSBT_TABBEDWINDOW
    // Not wired today because our Window background is an opaque solid — Mica wouldn't show through
    // without a broader alpha-aware visual rework. Left as a hook for a future pass.
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>
    /// Flips the native title bar and frame to dark so the caption stops clashing with the Catppuccin Mocha
    /// interior. Call from Window.SourceInitialized after the HWND is allocated.
    /// </summary>
    public static void ApplyDarkCaption(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        int value = 1;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)); }
        catch { /* pre-20H1 silently no-ops */ }

        SetAttribute(hwnd, DWMWA_CAPTION_COLOR, MochaBaseColorRef);
        SetAttribute(hwnd, DWMWA_TEXT_COLOR, MochaTextColorRef);
        SetAttribute(hwnd, DWMWA_BORDER_COLOR, MochaSurfaceColorRef);
    }

    private static void SetAttribute(IntPtr hwnd, int attribute, int value)
    {
        try { DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)); }
        catch { /* unsupported DWM attribute */ }
    }
}
