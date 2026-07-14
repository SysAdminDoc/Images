using ImageMagick;
using Images.Services;
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

    private static int SizeOfNested(Type parent, string name)
    {
        var type = parent.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(type);
        return Marshal.SizeOf(type!);
    }
}
