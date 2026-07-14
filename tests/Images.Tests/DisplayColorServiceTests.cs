using ImageMagick;
using Images.Services;
using Images.ViewModels;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Images.Tests;

public sealed class DisplayColorServiceTests
{
    [Fact]
    public void LegacySdrCustomRgbProfile_BecomesMonitorDestination()
    {
        var state = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY2",
            advancedColorKnown: true,
            advancedColorEnabled: false,
            profilePath: @"C:\Windows\System32\spool\drivers\color\WideGamut.icc",
            profileData: ColorProfiles.AppleRGB.ToByteArray(),
            bitsPerColorChannel: 8);

        Assert.True(state.UseLegacyMonitorProfile);
        Assert.Contains("WideGamut.icc", state.DestinationLabel, StringComparison.Ordinal);
        Assert.NotNull(state.ProfileFingerprint);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AdvancedColorOrUnknownState_UsesSrgbSafetyFallback(
        bool advancedColorKnown,
        bool advancedColorEnabled)
    {
        var state = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown,
            advancedColorEnabled,
            profilePath: @"C:\Color\WideGamut.icc",
            profileData: ColorProfiles.AppleRGB.ToByteArray());

        Assert.False(state.UseLegacyMonitorProfile);
        Assert.Equal("sRGB", state.DestinationLabel);
    }

    [Fact]
    public void WindowsDefaultSrgbProfile_DoesNotMasqueradeAsCustomMonitorOutput()
    {
        var state = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown: true,
            advancedColorEnabled: false,
            profilePath: @"C:\Windows\System32\spool\drivers\color\sRGB Color Space Profile.icm",
            profileData: ColorProfiles.SRGB.ToByteArray());

        Assert.True(state.IsStandardSrgbProfile);
        Assert.False(state.UseLegacyMonitorProfile);
        Assert.Equal("sRGB", state.DestinationLabel);
    }

    [Fact]
    public void InvalidProfile_IsRejectedBeforeDecode()
    {
        var state = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown: true,
            advancedColorEnabled: false,
            profilePath: @"C:\Color\broken.icc",
            profileData: [1, 2, 3, 4]);

        Assert.False(state.UseLegacyMonitorProfile);
        Assert.Null(state.ProfileData);
        Assert.Contains("invalid", state.ProbeDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EquivalentStates_CompareProfileContentRatherThanArrayIdentity()
    {
        var bytes = ColorProfiles.AppleRGB.ToByteArray();
        var first = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1", true, false, @"C:\Color\display.icc", bytes);
        var second = DisplayColorService.CreateStateForTest(
            @"\\.\display1", true, false, @"c:\color\DISPLAY.icc", [.. bytes]);

        Assert.True(first.IsEquivalentTo(second));
    }

    [Fact]
    public void NativeDisplayConfigLayouts_MatchWindowsSdkAbi()
    {
        var native = typeof(DisplayColorService).GetNestedType("NativeMethods", BindingFlags.NonPublic);
        Assert.NotNull(native);

        Assert.Equal(72, SizeOfNested(native!, "DisplayConfigPathInfo"));
        Assert.Equal(72, SizeOfNested(native!, "DisplayConfigModeInfo"));
        Assert.Equal(84, SizeOfNested(native!, "DisplayConfigSourceDeviceName"));
        Assert.Equal(32, SizeOfNested(native!, "DisplayConfigGetAdvancedColorInfo"));
    }

    [Fact]
    public void NativeDxgiOutputDescriptionLayout_MatchesWindowsSdkAbi()
    {
        Assert.Equal(
            IntPtr.Size == 8 ? 152 : 144,
            Marshal.SizeOf<HdrDisplayCapabilityProbe.DxgiOutputDescription1>());
    }

    [Fact]
    public void PqOutputDescription_ReportsHdrAndLuminanceEnvelope()
    {
        var capability = HdrDisplayCapabilityProbe.FromDescription(
            new HdrDisplayCapabilityProbe.DxgiOutputDescription1
            {
                DeviceName = string.Empty,
                BitsPerColor = 10,
                ColorSpace = 12,
                MinLuminance = 0.005f,
                MaxLuminance = 1000f,
                MaxFullFrameLuminance = 600f
            });

        Assert.True(capability.Known);
        Assert.True(capability.Active);
        Assert.Equal("RGB PQ (BT.2020)", capability.ColorSpace);
        Assert.Equal(10u, capability.BitsPerColor);
        Assert.Equal(1000f, capability.MaxLuminance);
        Assert.Equal(600f, capability.MaxFullFrameLuminance);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(12, true)]
    [InlineData(18, true)]
    [InlineData(17, false)]
    public void ColorSpaceClassification_OnlyMarksPqAndHlgAsHdr(uint colorSpace, bool expected)
        => Assert.Equal(expected, HdrDisplayCapabilityProbe.IsHdrColorSpace(colorSpace));

    [Fact]
    public void HdrTonemapBadge_RequiresBothHdrOutputAndTonemappedImage()
    {
        var hdr = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown: true,
            advancedColorEnabled: true,
            profilePath: null,
            profileData: null,
            hdrCapabilitiesKnown: true,
            hdrActive: true,
            hdrColorSpace: "RGB PQ (BT.2020)",
            hdrBitsPerColor: 10,
            hdrMaxLuminance: 1000,
            hdrMaxFullFrameLuminance: 600);
        var sdr = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown: true,
            advancedColorEnabled: false,
            profilePath: null,
            profileData: null,
            hdrCapabilitiesKnown: true,
            hdrActive: false,
            hdrColorSpace: "sRGB (BT.709)");

        Assert.Equal(
            "HDR display detected — this image is shown tonemapped to SDR",
            MainViewModel.BuildHdrDisplayStatus("Magick.NET · Reinhard tonemapped to SDR", hdr));
        Assert.Null(MainViewModel.BuildHdrDisplayStatus("Magick.NET", hdr));
        Assert.Null(MainViewModel.BuildHdrDisplayStatus("Magick.NET · Reinhard tonemapped to SDR", sdr));
    }

    [Fact]
    public void DxgiProbe_CurrentMonitor_FailsClosedWithoutThrowing()
    {
        var monitor = MonitorFromPoint(default, 2);

        var capability = HdrDisplayCapabilityProbe.Probe(monitor);

        Assert.NotNull(capability);
        Assert.False(string.IsNullOrWhiteSpace(capability.Detail));
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        internal readonly int X;
        internal readonly int Y;
    }

    private static int SizeOfNested(Type parent, string name)
    {
        var type = parent.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(type);
        return Marshal.SizeOf(type!);
    }
}
