using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// Resolves the legacy Windows display ICC profile for the monitor containing the main window.
/// A monitor profile is only safe as an application-side output target while Advanced Color is
/// inactive; when Windows is managing the desktop color space, Images keeps its output in sRGB.
/// </summary>
internal static class DisplayColorService
{
    private const long MaxProfileBytes = 32L * 1024 * 1024;
    private static readonly object Gate = new();
    private static DisplayColorState _current = DisplayColorState.Unprobed;
    private static nint _currentMonitor;

    internal static DisplayColorState Current => Volatile.Read(ref _current);

    /// <summary>
    /// Refreshes the profile for the monitor that owns most of <paramref name="hwnd"/>. Returns
    /// true only when the effective color destination changed, so callers can avoid needless
    /// re-decodes during ordinary window movement on the same display.
    /// </summary>
    internal static bool RefreshForWindow(nint hwnd, bool force = false)
    {
        if (hwnd == 0)
            return false;

        nint monitor;
        try
        {
            monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Update(0, DisplayColorState.Unavailable("Windows display color APIs are unavailable."));
        }

        if (monitor == 0)
            return Update(0, DisplayColorState.Unavailable("Windows could not resolve the active monitor."));

        lock (Gate)
        {
            if (!force && monitor == _currentMonitor)
                return false;
        }

        var next = Probe(monitor);
        return Update(monitor, next);
    }

    internal static DisplayColorState CreateStateForTest(
        string deviceName,
        bool advancedColorKnown,
        bool advancedColorEnabled,
        string? profilePath,
        byte[]? profileData,
        uint colorEncoding = 0,
        uint bitsPerColorChannel = 8,
        string? probeDetail = null,
        bool hdrCapabilitiesKnown = false,
        bool hdrActive = false,
        string hdrColorSpace = "Unknown",
        uint hdrBitsPerColor = 0,
        float hdrMinLuminance = 0,
        float hdrMaxLuminance = 0,
        float hdrMaxFullFrameLuminance = 0)
        => BuildState(
            deviceName,
            advancedColorKnown,
            advancedColorEnabled,
            profilePath,
            profileData,
            colorEncoding,
            bitsPerColorChannel,
            probeDetail ?? "Test display state.",
            new HdrDisplayCapability(
                hdrCapabilitiesKnown,
                hdrActive,
                hdrColorSpace,
                hdrBitsPerColor,
                hdrMinLuminance,
                hdrMaxLuminance,
                hdrMaxFullFrameLuminance,
                hdrCapabilitiesKnown ? "Test DXGI display state." : "Test DXGI state unavailable."));

    private static bool Update(nint monitor, DisplayColorState next)
    {
        lock (Gate)
        {
            var previous = _current;
            _currentMonitor = monitor;
            if (previous.IsEquivalentTo(next))
                return false;

            Volatile.Write(ref _current, next);
            return true;
        }
    }

    private static DisplayColorState Probe(nint monitor)
    {
        try
        {
            var monitorInfo = NativeMethods.MonitorInfoEx.Create();
            if (!NativeMethods.GetMonitorInfoW(monitor, ref monitorInfo))
                return DisplayColorState.Unavailable("Windows could not read the active monitor identity.");

            var deviceName = monitorInfo.DeviceName.TrimEnd('\0');
            var advancedKnown = TryGetAdvancedColorInfo(
                deviceName,
                out var advancedEnabled,
                out var colorEncoding,
                out var bitsPerColorChannel);

            var profilePath = TryGetMonitorProfilePath(deviceName);
            var (profileData, profileReadDetail) = TryReadProfile(profilePath);
            var hdrCapability = HdrDisplayCapabilityProbe.Probe(monitor);
            var advancedDetail = advancedKnown
                ? advancedEnabled ? "Windows Advanced Color is active." : "Windows Advanced Color is off."
                : "Windows Advanced Color state could not be verified.";

            return BuildState(
                deviceName,
                advancedKnown,
                advancedEnabled,
                profilePath,
                profileData,
                colorEncoding,
                bitsPerColorChannel,
                $"{advancedDetail} {profileReadDetail} {hdrCapability.Detail}",
                hdrCapability);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return DisplayColorState.Unavailable($"Display color probing failed: {ex.GetType().Name}.");
        }
    }

    private static DisplayColorState BuildState(
        string deviceName,
        bool advancedColorKnown,
        bool advancedColorEnabled,
        string? profilePath,
        byte[]? profileData,
        uint colorEncoding,
        uint bitsPerColorChannel,
        string probeDetail,
        HdrDisplayCapability hdrCapability)
    {
        string? description = null;
        string? fingerprint = null;
        var validatedData = profileData;

        if (validatedData is { Length: > 0 })
        {
            try
            {
                var profile = new ColorProfile(validatedData);
                if (profile.ColorSpace is not ColorSpace.RGB and not ColorSpace.sRGB)
                {
                    validatedData = null;
                    probeDetail += " The selected display profile is not RGB and was ignored.";
                }
                else
                {
                    description = NormalizeProfileDescription(profile.Description);
                    fingerprint = Convert.ToHexString(SHA256.HashData(validatedData));
                }
            }
            catch (Exception ex) when (ex is MagickException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                validatedData = null;
                probeDetail += $" The selected display profile was invalid ({ex.GetType().Name}) and was ignored.";
            }
        }

        var fileName = string.IsNullOrWhiteSpace(profilePath) ? null : Path.GetFileName(profilePath);
        var isStandardSrgb = IsStandardSrgbProfile(fileName);

        return new DisplayColorState(
            string.IsNullOrWhiteSpace(deviceName) ? "Unknown display" : deviceName,
            advancedColorKnown,
            advancedColorEnabled,
            ColorEncodingName(colorEncoding),
            bitsPerColorChannel,
            hdrCapability.Known,
            hdrCapability.Active,
            hdrCapability.ColorSpace,
            hdrCapability.BitsPerColor,
            hdrCapability.MinLuminance,
            hdrCapability.MaxLuminance,
            hdrCapability.MaxFullFrameLuminance,
            profilePath,
            fileName,
            description,
            validatedData,
            fingerprint,
            isStandardSrgb,
            probeDetail);
    }

    private static string NormalizeProfileDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private static bool IsStandardSrgbProfile(string? fileName)
        => fileName is not null
           && (fileName.Equals("sRGB Color Space Profile.icm", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("sRGB IEC61966-2.1.icc", StringComparison.OrdinalIgnoreCase));

    private static (byte[]? Data, string Detail) TryReadProfile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (null, "No monitor ICC profile was reported; sRGB fallback is active.");

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
                return (null, "The reported monitor ICC profile is missing; sRGB fallback is active.");
            if (file.Length <= 0 || file.Length > MaxProfileBytes)
                return (null, "The reported monitor ICC profile has an unsafe size; sRGB fallback is active.");

            using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var data = new byte[checked((int)stream.Length)];
            stream.ReadExactly(data);
            return (data, $"Monitor ICC profile: {file.Name}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (null, $"The monitor ICC profile could not be read ({ex.GetType().Name}); sRGB fallback is active.");
        }
    }

    private static string? TryGetMonitorProfilePath(string deviceName)
    {
        nint hdc = 0;
        try
        {
            hdc = NativeMethods.CreateDCW(deviceName, deviceName, 0, 0);
            if (hdc == 0)
                return null;

            uint length = 0;
            _ = NativeMethods.GetICMProfileW(hdc, ref length, null);
            if (length is <= 1 or > 32_768)
                return null;

            var buffer = new StringBuilder(checked((int)length));
            return NativeMethods.GetICMProfileW(hdc, ref length, buffer)
                ? buffer.ToString()
                : null;
        }
        finally
        {
            if (hdc != 0)
                _ = NativeMethods.DeleteDC(hdc);
        }
    }

    private static bool TryGetAdvancedColorInfo(
        string deviceName,
        out bool enabled,
        out uint colorEncoding,
        out uint bitsPerColorChannel)
    {
        enabled = false;
        colorEncoding = 0;
        bitsPerColorChannel = 0;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var status = NativeMethods.GetDisplayConfigBufferSizes(
                NativeMethods.QdcOnlyActivePaths,
                out var pathCount,
                out var modeCount);
            if (status != NativeMethods.ErrorSuccess || pathCount == 0)
                return false;

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[modeCount];
            status = NativeMethods.QueryDisplayConfig(
                NativeMethods.QdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                0);

            if (status == NativeMethods.ErrorInsufficientBuffer)
                continue;
            if (status != NativeMethods.ErrorSuccess)
                return false;

            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var sourceName = NativeMethods.DisplayConfigSourceDeviceName.Create(path.SourceInfo.AdapterId, path.SourceInfo.Id);
                if (NativeMethods.DisplayConfigGetSourceDeviceInfo(ref sourceName) != NativeMethods.ErrorSuccess
                    || !sourceName.ViewGdiDeviceName.TrimEnd('\0').Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var advanced = NativeMethods.DisplayConfigGetAdvancedColorInfo.Create(
                    path.TargetInfo.AdapterId,
                    path.TargetInfo.Id);
                if (NativeMethods.GetAdvancedColorDeviceInfo(ref advanced) != NativeMethods.ErrorSuccess)
                    return false;

                enabled = (advanced.Value & 0b10u) != 0;
                colorEncoding = advanced.ColorEncoding;
                bitsPerColorChannel = advanced.BitsPerColorChannel;
                return true;
            }

            return false;
        }

        return false;
    }

    private static string ColorEncodingName(uint value)
        => value switch
        {
            0 => "RGB",
            1 => "YCbCr 4:4:4",
            2 => "YCbCr 4:2:2",
            3 => "YCbCr 4:2:0",
            4 => "Intensity",
            _ => $"Unknown ({value})"
        };

    private static class NativeMethods
    {
        internal const uint MonitorDefaultToNearest = 2;
        internal const uint QdcOnlyActivePaths = 2;
        internal const int ErrorSuccess = 0;
        internal const int ErrorInsufficientBuffer = 122;
        private const uint DisplayConfigDeviceInfoGetSourceName = 1;
        private const uint DisplayConfigDeviceInfoGetAdvancedColorInfo = 9;

        [DllImport("user32.dll")]
        internal static extern nint MonitorFromWindow(nint hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfoEx monitorInfo);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateDCW(string driver, string device, nint port, nint deviceMode);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(nint hdc);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetICMProfileW(nint hdc, ref uint bufferSize, StringBuilder? fileName);

        [DllImport("user32.dll")]
        internal static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        internal static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [In, Out] DisplayConfigPathInfo[] paths,
            ref uint modeCount,
            [In, Out] DisplayConfigModeInfo[] modes,
            nint currentTopologyId);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        internal static extern int DisplayConfigGetSourceDeviceInfo(ref DisplayConfigSourceDeviceName request);

        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
        internal static extern int GetAdvancedColorDeviceInfo(ref DisplayConfigGetAdvancedColorInfo request);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MonitorInfoEx
        {
            internal uint Size;
            internal NativeRect Monitor;
            internal NativeRect WorkArea;
            internal uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            internal string DeviceName;

            internal static MonitorInfoEx Create()
                => new()
                {
                    Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
                    DeviceName = string.Empty
                };
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeRect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Luid
        {
            internal uint LowPart;
            internal int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigPathSourceInfo
        {
            internal Luid AdapterId;
            internal uint Id;
            internal uint ModeInfoIndex;
            internal uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigRational
        {
            internal uint Numerator;
            internal uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigPathTargetInfo
        {
            internal Luid AdapterId;
            internal uint Id;
            internal uint ModeInfoIndex;
            internal uint OutputTechnology;
            internal uint Rotation;
            internal uint Scaling;
            internal DisplayConfigRational RefreshRate;
            internal uint ScanLineOrdering;
            internal int TargetAvailable;
            internal uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigPathInfo
        {
            internal DisplayConfigPathSourceInfo SourceInfo;
            internal DisplayConfigPathTargetInfo TargetInfo;
            internal uint Flags;
        }

        // The largest native union member is DISPLAYCONFIG_TARGET_MODE (56 bytes).
        [StructLayout(LayoutKind.Explicit, Size = 72)]
        internal struct DisplayConfigModeInfo
        {
            [FieldOffset(0)] internal uint InfoType;
            [FieldOffset(4)] internal uint Id;
            [FieldOffset(8)] internal Luid AdapterId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigDeviceInfoHeader
        {
            internal uint Type;
            internal uint Size;
            internal Luid AdapterId;
            internal uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DisplayConfigSourceDeviceName
        {
            internal DisplayConfigDeviceInfoHeader Header;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            internal string ViewGdiDeviceName;

            internal static DisplayConfigSourceDeviceName Create(Luid adapterId, uint id)
                => new()
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DisplayConfigDeviceInfoGetSourceName,
                        Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                        AdapterId = adapterId,
                        Id = id
                    },
                    ViewGdiDeviceName = string.Empty
                };
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DisplayConfigGetAdvancedColorInfo
        {
            internal DisplayConfigDeviceInfoHeader Header;
            internal uint Value;
            internal uint ColorEncoding;
            internal uint BitsPerColorChannel;

            internal static DisplayConfigGetAdvancedColorInfo Create(Luid adapterId, uint id)
                => new()
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DisplayConfigDeviceInfoGetAdvancedColorInfo,
                        Size = (uint)Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(),
                        AdapterId = adapterId,
                        Id = id
                    }
                };
        }
    }
}

internal sealed record DisplayColorState(
    string DeviceName,
    bool AdvancedColorKnown,
    bool AdvancedColorEnabled,
    string ColorEncoding,
    uint BitsPerColorChannel,
    bool HdrCapabilitiesKnown,
    bool HdrActive,
    string HdrColorSpace,
    uint HdrBitsPerColor,
    float HdrMinLuminance,
    float HdrMaxLuminance,
    float HdrMaxFullFrameLuminance,
    string? ProfilePath,
    string? ProfileFileName,
    string? ProfileDescription,
    byte[]? ProfileData,
    string? ProfileFingerprint,
    bool IsStandardSrgbProfile,
    string ProbeDetail)
{
    internal static DisplayColorState Unprobed { get; } = Unavailable("Display color has not been probed yet.");

    internal bool UseLegacyMonitorProfile
        => AdvancedColorKnown
           && !AdvancedColorEnabled
           && !IsStandardSrgbProfile
           && ProfileData is { Length: > 0 };

    internal string DestinationLabel
        => UseLegacyMonitorProfile
            ? $"monitor ICC ({ProfileFileName ?? ProfileDescription ?? "custom profile"})"
            : "sRGB";

    internal bool IsEquivalentTo(DisplayColorState other)
        => DeviceName.Equals(other.DeviceName, StringComparison.OrdinalIgnoreCase)
           && AdvancedColorKnown == other.AdvancedColorKnown
           && AdvancedColorEnabled == other.AdvancedColorEnabled
           && ColorEncoding == other.ColorEncoding
           && BitsPerColorChannel == other.BitsPerColorChannel
           && HdrCapabilitiesKnown == other.HdrCapabilitiesKnown
           && HdrActive == other.HdrActive
           && HdrColorSpace == other.HdrColorSpace
           && HdrBitsPerColor == other.HdrBitsPerColor
           && HdrMinLuminance.Equals(other.HdrMinLuminance)
           && HdrMaxLuminance.Equals(other.HdrMaxLuminance)
           && HdrMaxFullFrameLuminance.Equals(other.HdrMaxFullFrameLuminance)
           && string.Equals(ProfilePath, other.ProfilePath, StringComparison.OrdinalIgnoreCase)
           && string.Equals(ProfileFingerprint, other.ProfileFingerprint, StringComparison.Ordinal);

    internal static DisplayColorState Unavailable(string detail)
        => new(
            "Unknown display",
            AdvancedColorKnown: false,
            AdvancedColorEnabled: false,
            ColorEncoding: "Unknown",
            BitsPerColorChannel: 0,
            HdrCapabilitiesKnown: false,
            HdrActive: false,
            HdrColorSpace: "Unknown",
            HdrBitsPerColor: 0,
            HdrMinLuminance: 0,
            HdrMaxLuminance: 0,
            HdrMaxFullFrameLuminance: 0,
            ProfilePath: null,
            ProfileFileName: null,
            ProfileDescription: null,
            ProfileData: null,
            ProfileFingerprint: null,
            IsStandardSrgbProfile: false,
            ProbeDetail: detail);
}
