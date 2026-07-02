using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Images.Services;

/// <summary>
/// V20-27: multi-monitor helpers using P/Invoke to user32.dll. Enumerates connected monitors,
/// identifies which monitor a WPF window occupies, and can move a window to a target monitor.
/// All coordinates are in logical (DIU) space so WPF scaling is handled transparently.
/// </summary>
public static class MonitorService
{
    // ---- P/Invoke constants ----
    private const uint MonitorDefaultToNearest = 2;

    // ---- P/Invoke signatures ----

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeRect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    // ---- Native structures ----

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public static MonitorInfoEx Create()
        {
            var info = new MonitorInfoEx();
            info.Size = Marshal.SizeOf(typeof(MonitorInfoEx));
            info.DeviceName = string.Empty;
            return info;
        }
    }

    // ---- Public types ----

    /// <summary>
    /// Describes a connected display monitor with its device name and work area in physical pixels.
    /// </summary>
    public sealed class MonitorInfo
    {
        /// <summary>Device name, e.g. <c>\\.\DISPLAY1</c>.</summary>
        public string DeviceName { get; init; } = "";

        /// <summary>One-based display number extracted from the device name for user-facing labels.</summary>
        public int DisplayNumber { get; init; }

        /// <summary>Work area (excludes taskbar) in physical pixels.</summary>
        public Rect WorkAreaPhysical { get; init; }

        /// <summary>Full monitor bounds in physical pixels.</summary>
        public Rect BoundsPhysical { get; init; }

        /// <summary>True when this is the primary display.</summary>
        public bool IsPrimary { get; init; }

        internal IntPtr Handle { get; init; }
    }

    // ---- Public API ----

    /// <summary>
    /// Returns all connected monitors, ordered by physical left position (left-to-right across the
    /// desktop). Numbering is extracted from the device name, e.g. <c>\\.\DISPLAY2</c> yields 2.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeRect _, IntPtr _) =>
        {
            var info = MonitorInfoEx.Create();
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo
                {
                    DeviceName = info.DeviceName.TrimEnd('\0'),
                    DisplayNumber = ParseDisplayNumber(info.DeviceName),
                    WorkAreaPhysical = ToRect(info.WorkArea),
                    BoundsPhysical = ToRect(info.Monitor),
                    IsPrimary = (info.Flags & 1) == 1,
                    Handle = hMonitor,
                });
            }
            return true;
        }, IntPtr.Zero);

        monitors.Sort((a, b) => a.BoundsPhysical.Left.CompareTo(b.BoundsPhysical.Left));
        return monitors;
    }

    /// <summary>
    /// Returns the device name of the monitor that currently contains the majority of the given
    /// WPF window, or <c>null</c> if the handle is unavailable (e.g. before SourceInitialized).
    /// </summary>
    public static string? GetCurrentMonitorDeviceName(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return null;

        var hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = MonitorInfoEx.Create();
        return GetMonitorInfo(hMonitor, ref info)
            ? info.DeviceName.TrimEnd('\0')
            : null;
    }

    /// <summary>
    /// Returns the work area of the monitor the window is currently on, in physical pixels.
    /// Falls back to <see cref="SystemParameters.WorkArea"/> (primary monitor, logical units)
    /// if the P/Invoke call fails.
    /// </summary>
    public static Rect GetCurrentMonitorWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return SystemParameters.WorkArea;

        var hMonitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = MonitorInfoEx.Create();
        return GetMonitorInfo(hMonitor, ref info)
            ? ToRect(info.WorkArea)
            : SystemParameters.WorkArea;
    }

    /// <summary>
    /// Moves the given window so it is centered on the target monitor's work area. Converts
    /// physical pixels to WPF logical units using the window's DPI. If maximized, the window is
    /// first restored so the move is visible.
    /// </summary>
    public static void MoveWindowToMonitor(Window window, MonitorInfo target)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var dpiScale = GetMonitorDpiScale(target.Handle, hwnd);

        var waLeft = target.WorkAreaPhysical.Left / dpiScale;
        var waTop = target.WorkAreaPhysical.Top / dpiScale;
        var waWidth = target.WorkAreaPhysical.Width / dpiScale;
        var waHeight = target.WorkAreaPhysical.Height / dpiScale;

        var wasMaximized = window.WindowState == WindowState.Maximized;
        if (wasMaximized)
            window.WindowState = WindowState.Normal;

        // Clamp the current size to the target work area.
        var w = Math.Min(window.Width, waWidth);
        var h = Math.Min(window.Height, waHeight);

        window.Left = waLeft + (waWidth - w) / 2;
        window.Top = waTop + (waHeight - h) / 2;
        window.Width = w;
        window.Height = h;

        if (wasMaximized)
            window.WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Generates a settings key suffix that uniquely identifies a monitor. Uses the device name
    /// which is stable across sessions (e.g. <c>\\.\DISPLAY1</c>). Sanitized to be safe as a
    /// settings key segment.
    /// </summary>
    public static string SanitizeDeviceName(string deviceName)
    {
        // "\\.\DISPLAY1" -> "DISPLAY1"
        var clean = deviceName.Replace(@"\\.\", "").Replace(@"\\", "").Replace(@"\", "").Replace(".", "");
        return string.IsNullOrWhiteSpace(clean) ? "DEFAULT" : clean;
    }

    // ---- Helpers ----

    private static double GetMonitorDpiScale(IntPtr hMonitor, IntPtr hwndFallback)
    {
        if (hMonitor != IntPtr.Zero)
        {
            try
            {
                if (GetDpiForMonitor(hMonitor, 0, out var dpiX, out _) == 0 && dpiX > 0)
                    return dpiX / 96.0;
            }
            catch { }
        }

        return GetDpiScale(hwndFallback);
    }

    private static double GetDpiScale(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return 1.0;

        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch
        {
            // GetDpiForWindow requires Windows 10 1607+. Fall back gracefully.
            return 1.0;
        }
    }

    private static Rect ToRect(NativeRect r)
        => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    private static int ParseDisplayNumber(string deviceName)
    {
        // Typical: "\\.\DISPLAY1", "\\.\DISPLAY2". Extract trailing digits.
        var trimmed = deviceName.TrimEnd('\0');
        var i = trimmed.Length - 1;
        while (i >= 0 && char.IsDigit(trimmed[i])) i--;
        return int.TryParse(trimmed.AsSpan(i + 1), out var n) ? n : 0;
    }
}
