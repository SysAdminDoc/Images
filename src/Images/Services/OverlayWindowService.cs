using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Images.Services;

public static class OverlayWindowService
{
    public const int ExitHotKeyId = 0x4D24;
    public const int WmHotKey = 0x0312;

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    public const string ExitHotKeyText = "Ctrl+Alt+O";

    public static int BuildExtendedStyle(int currentStyle, bool clickThrough)
        => clickThrough
            ? currentStyle | WsExTransparent | WsExLayered
            : currentStyle & ~WsExTransparent;

    public static bool IsClickThroughStyle(int style)
        => (style & WsExTransparent) == WsExTransparent;

    public static void ApplyClickThrough(IntPtr hwnd, bool clickThrough)
    {
        if (hwnd == IntPtr.Zero)
            return;

        var current = GetWindowLong(hwnd, GwlExStyle);
        var next = BuildExtendedStyle(current, clickThrough);
        if (next != current)
            SetWindowLong(hwnd, GwlExStyle, next);
    }

    public static bool RegisterExitHotKey(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        var key = (uint)KeyInterop.VirtualKeyFromKey(Key.O);
        return RegisterHotKey(hwnd, ExitHotKeyId, ModControl | ModAlt, key);
    }

    public static void UnregisterExitHotKey(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        UnregisterHotKey(hwnd, ExitHotKeyId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
