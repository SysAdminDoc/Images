using System.Runtime.InteropServices;

namespace Images.Services;

/// <summary>
/// Reads the active output color space and luminance envelope without creating a D3D device or
/// swap chain. DXGI reports the current desktop mode, so this is an honest status signal rather
/// than a claim that the WPF surface can present native HDR pixels.
/// </summary>
internal static class HdrDisplayCapabilityProbe
{
    private const int S_OK = 0;
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const int FactoryEnumAdapters1VtableIndex = 12;
    private const int AdapterEnumOutputsVtableIndex = 7;
    private const int OutputGetDescVtableIndex = 7;
    private const int Output6GetDesc1VtableIndex = 27;
    private const int MaxAdapters = 32;
    private const int MaxOutputsPerAdapter = 64;

    private static readonly Guid Factory1Guid = new("770AAE78-F26F-4DBA-A829-253C83D1B387");
    private static readonly Guid Output6Guid = new("068346E8-AAEC-4B84-ADD7-137F513F77A1");

    internal static HdrDisplayCapability Probe(nint monitor)
    {
        if (monitor == 0)
            return HdrDisplayCapability.Unavailable("DXGI monitor identity is unavailable.");

        nint factory = 0;
        try
        {
            var factoryGuid = Factory1Guid;
            var result = NativeMethods.CreateDXGIFactory1(ref factoryGuid, out factory);
            if (result < 0 || factory == 0)
                return HdrDisplayCapability.Unavailable($"DXGI factory creation failed (0x{result:X8}).");

            var enumAdapters = GetDelegate<EnumAdapters1Delegate>(factory, FactoryEnumAdapters1VtableIndex);
            for (uint adapterIndex = 0; adapterIndex < MaxAdapters; adapterIndex++)
            {
                nint adapter = 0;
                result = enumAdapters(factory, adapterIndex, out adapter);
                if (result == DxgiErrorNotFound)
                    break;
                if (result < 0 || adapter == 0)
                    continue;

                try
                {
                    var capability = ProbeAdapter(adapter, monitor);
                    if (capability is not null)
                        return capability;
                }
                finally
                {
                    Release(adapter);
                }
            }

            return HdrDisplayCapability.Unavailable("DXGI could not match the active monitor to an output.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
                                       MarshalDirectiveException or InvalidCastException)
        {
            return HdrDisplayCapability.Unavailable($"DXGI HDR probing is unavailable ({ex.GetType().Name}).");
        }
        finally
        {
            Release(factory);
        }
    }

    private static HdrDisplayCapability? ProbeAdapter(nint adapter, nint monitor)
    {
        var enumOutputs = GetDelegate<EnumOutputsDelegate>(adapter, AdapterEnumOutputsVtableIndex);
        for (uint outputIndex = 0; outputIndex < MaxOutputsPerAdapter; outputIndex++)
        {
            nint output = 0;
            var result = enumOutputs(adapter, outputIndex, out output);
            if (result == DxgiErrorNotFound)
                return null;
            if (result < 0 || output == 0)
                continue;

            try
            {
                var getDesc = GetDelegate<GetOutputDescDelegate>(output, OutputGetDescVtableIndex);
                if (getDesc(output, out var basicDescription) < 0 || basicDescription.Monitor != monitor)
                    continue;

                return ProbeOutput6(output);
            }
            finally
            {
                Release(output);
            }
        }

        return null;
    }

    private static HdrDisplayCapability ProbeOutput6(nint output)
    {
        nint output6 = 0;
        try
        {
            var queryInterface = GetDelegate<QueryInterfaceDelegate>(output, 0);
            var output6Guid = Output6Guid;
            var result = queryInterface(output, ref output6Guid, out output6);
            if (result < 0 || output6 == 0)
                return HdrDisplayCapability.Unavailable("The active output does not expose IDXGIOutput6.");

            var getDesc1 = GetDelegate<GetOutputDesc1Delegate>(output6, Output6GetDesc1VtableIndex);
            result = getDesc1(output6, out var description);
            if (result < 0)
                return HdrDisplayCapability.Unavailable($"DXGI output description failed (0x{result:X8}).");

            return FromDescription(description);
        }
        finally
        {
            Release(output6);
        }
    }

    internal static HdrDisplayCapability FromDescription(DxgiOutputDescription1 description)
    {
        var colorSpace = DescribeColorSpace(description.ColorSpace);
        var hdrActive = IsHdrColorSpace(description.ColorSpace);
        return new HdrDisplayCapability(
            Known: true,
            Active: hdrActive,
            ColorSpace: colorSpace,
            BitsPerColor: description.BitsPerColor,
            MinLuminance: SanitizeLuminance(description.MinLuminance),
            MaxLuminance: SanitizeLuminance(description.MaxLuminance),
            MaxFullFrameLuminance: SanitizeLuminance(description.MaxFullFrameLuminance),
            Detail: hdrActive
                ? "DXGI reports an active HDR desktop color space."
                : "DXGI reports an SDR desktop color space.");
    }

    internal static bool IsHdrColorSpace(uint value)
        => value is 12 or 13 or 14 or 16 or 18 or 19;

    internal static string DescribeColorSpace(uint value) => value switch
    {
        0 => "sRGB (BT.709)",
        12 => "RGB PQ (BT.2020)",
        13 => "YCbCr PQ (BT.2020)",
        14 => "RGB studio PQ (BT.2020)",
        16 => "YCbCr top-left PQ (BT.2020)",
        17 => "RGB gamma 2.2 (BT.2020)",
        18 => "YCbCr studio HLG (BT.2020)",
        19 => "YCbCr full HLG (BT.2020)",
        25 => "RGB linear (BT.2020)",
        uint.MaxValue => "Custom",
        _ => $"DXGI color space {value}"
    };

    private static float SanitizeLuminance(float value)
        => float.IsFinite(value) && value >= 0 ? value : 0;

    private static TDelegate GetDelegate<TDelegate>(nint instance, int vtableIndex)
        where TDelegate : Delegate
    {
        var vtable = Marshal.ReadIntPtr(instance);
        var method = Marshal.ReadIntPtr(vtable, vtableIndex * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(method);
    }

    private static void Release(nint instance)
    {
        if (instance == 0)
            return;

        var release = GetDelegate<ReleaseDelegate>(instance, 2);
        _ = release(instance);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(nint instance, ref Guid interfaceId, out nint result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseDelegate(nint instance);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Delegate(nint instance, uint index, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumOutputsDelegate(nint instance, uint index, out nint output);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetOutputDescDelegate(nint instance, out DxgiOutputDescription description);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetOutputDesc1Delegate(nint instance, out DxgiOutputDescription1 description);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiOutputDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
        internal NativeRect DesktopCoordinates;
        internal int AttachedToDesktop;
        internal uint Rotation;
        internal nint Monitor;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DxgiOutputDescription1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
        internal NativeRect DesktopCoordinates;
        internal int AttachedToDesktop;
        internal uint Rotation;
        internal nint Monitor;
        internal uint BitsPerColor;
        internal uint ColorSpace;
        internal float RedPrimaryX;
        internal float RedPrimaryY;
        internal float GreenPrimaryX;
        internal float GreenPrimaryY;
        internal float BluePrimaryX;
        internal float BluePrimaryY;
        internal float WhitePointX;
        internal float WhitePointY;
        internal float MinLuminance;
        internal float MaxLuminance;
        internal float MaxFullFrameLuminance;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    private static class NativeMethods
    {
        [DllImport("dxgi.dll", ExactSpelling = true)]
        internal static extern int CreateDXGIFactory1(ref Guid interfaceId, out nint factory);
    }
}

internal sealed record HdrDisplayCapability(
    bool Known,
    bool Active,
    string ColorSpace,
    uint BitsPerColor,
    float MinLuminance,
    float MaxLuminance,
    float MaxFullFrameLuminance,
    string Detail)
{
    internal static HdrDisplayCapability Unavailable(string detail)
        => new(
            Known: false,
            Active: false,
            ColorSpace: "Unknown",
            BitsPerColor: 0,
            MinLuminance: 0,
            MaxLuminance: 0,
            MaxFullFrameLuminance: 0,
            Detail: detail);
}
