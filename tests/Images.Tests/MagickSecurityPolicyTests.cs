using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class MagickSecurityPolicyTests
{
    [Fact]
    public void Configure_AppliesExpectedResourceLimits()
    {
        var report = MagickSecurityPolicy.Configure(false, "Not found");

        Assert.True(report.IsEnforced, report.Summary);
        Assert.Equal(MagickSecurityPolicy.MemoryLimitBytes, report.MemoryLimitBytes);
        Assert.Equal(MagickSecurityPolicy.DiskLimitBytes, report.DiskLimitBytes);
        Assert.Equal(MagickSecurityPolicy.MaxMemoryRequestBytes, report.MaxMemoryRequestBytes);
        Assert.Equal(MagickSecurityPolicy.MaxProfileSizeBytes, report.MaxProfileSizeBytes);
        Assert.Equal((ulong)MagickSecurityPolicy.DimensionLimitPixels, report.WidthLimitPixels);
        Assert.Equal((ulong)MagickSecurityPolicy.DimensionLimitPixels, report.HeightLimitPixels);
        Assert.Equal(MagickSecurityPolicy.AreaLimitPixels, report.AreaLimitPixels);
        Assert.Equal(MagickSecurityPolicy.RenderDimensionLimitPixels, report.RenderDimensionLimitPixels);
        Assert.Equal((ulong)MagickSecurityPolicy.ListLengthLimit, report.ListLengthLimit);
        Assert.Equal((ulong)MagickSecurityPolicy.TimeLimitSeconds, report.TimeLimitSeconds);
        Assert.Equal((ulong)MagickSecurityPolicy.ThreadLimit, report.ThreadLimit);
        Assert.True(report.DelegatesDefaultDenied);
        Assert.Contains("PNG", report.AllowedReadCoders);
    }

    [Theory]
    [InlineData(".png", true)]
    [InlineData(".jpg", true)]
    [InlineData(".pdf", false)]
    [InlineData(".PDF", false)]
    [InlineData(".svg", false)]
    [InlineData(".mvg", false)]
    [InlineData(".https", false)]
    public void IsWriteTargetAllowed_BlocksHighRiskDelegateTargets(string extension, bool expected)
        => Assert.Equal(expected, MagickSecurityPolicy.IsWriteTargetAllowed(extension));

    [Fact]
    public void IsWriteFormatAllowed_BlocksDocumentAndVectorFormats()
    {
        Assert.True(MagickSecurityPolicy.IsWriteFormatAllowed(MagickFormat.Png));
        foreach (var extension in MagickSecurityPolicy.DisallowedWriteExtensions)
        {
            var formatName = extension.TrimStart('.');
            if (Enum.TryParse<MagickFormat>(formatName, ignoreCase: true, out var format))
                Assert.False(MagickSecurityPolicy.IsWriteFormatAllowed(format), extension);
        }
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(30000, 30000, true)]
    [InlineData(30001, 1, false)]
    [InlineData(1, 30001, false)]
    [InlineData(0, 1, false)]
    public void IsRenderableDimensions_RejectsHugeOrInvalidDimensions(long width, long height, bool expected)
        => Assert.Equal(expected, MagickSecurityPolicy.IsRenderableDimensions(width, height));

    [Fact]
    public void Inspect_ReportsDocumentDelegateGate()
    {
        var report = MagickSecurityPolicy.Configure(false, "Not found");

        Assert.Contains("delegates are denied", report.DocumentDelegateStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".pdf", report.BlockedWriteSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("memory", report.ResourceLimitSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ paths disabled", report.ReadPolicySummary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PNG", true)]
    [InlineData("JPEG", true)]
    [InlineData("SVG", true)]
    [InlineData("MSL", false)]
    [InlineData("MVG", true)]
    [InlineData("MNG", false)]
    [InlineData("TIM", false)]
    [InlineData("URL", false)]
    public void IsReadCoderAllowed_UsesExplicitAllowlist(string coder, bool expected)
        => Assert.Equal(expected, MagickSecurityPolicy.IsReadCoderAllowed(coder));

    [Fact]
    public void CreateNativePolicyXml_DeniesBeforePermittingReviewedCoders()
    {
        var xml = MagickSecurityPolicy.CreateNativePolicyXml();

        var denyCoder = xml.IndexOf("domain=\"coder\" rights=\"none\" pattern=\"*\"", StringComparison.Ordinal);
        var allowCoder = xml.IndexOf("domain=\"coder\" rights=\"read | write\"", StringComparison.Ordinal);
        Assert.True(denyCoder >= 0);
        Assert.True(allowCoder > denyCoder);
        Assert.Contains("domain=\"delegate\" rights=\"none\" pattern=\"*\"", xml, StringComparison.Ordinal);
        Assert.Contains("domain=\"filter\" rights=\"none\" pattern=\"*\"", xml, StringComparison.Ordinal);
        Assert.Contains("domain=\"path\" rights=\"none\" pattern=\"@*\"", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("MSL", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("MNG", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("TIM", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateNativePolicyXml_WithGhostscript_AllowsOnlyRasterPreviewDelegates()
    {
        var xml = MagickSecurityPolicy.CreateNativePolicyXml(allowDocumentDelegates: true);

        Assert.Contains("rights=\"none\" pattern=\"*\"", xml, StringComparison.Ordinal);
        foreach (var name in MagickSecurityPolicy.AllowedDocumentDelegates)
            Assert.Contains(name, xml, StringComparison.Ordinal);
        Assert.DoesNotContain("inkscape", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rsvg", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("unsafe.mng")]
    [InlineData("unsafe.tim")]
    [InlineData("unsafe.mvg")]
    [InlineData("unsafe.msvg")]
    public void HazardousNativeCoders_AreNotAdvertisedAsUserInputs(string path)
        => Assert.False(SupportedImageFormats.IsSupported(path));

    [Fact]
    public void Configure_AllowsPngDecodeAndRejectsBlockedCoderBeforeDecode()
    {
        MagickSecurityPolicy.Configure(false, "Not found");

        using var source = new MagickImage(MagickColors.CornflowerBlue, 3, 2);
        var png = source.ToByteArray(MagickFormat.Png);
        using var decoded = new MagickImage(png);
        Assert.Equal(MagickFormat.Png, decoded.Format);

        using var temp = TestDirectory.Create();
        var msl = temp.WriteFile(
            "blocked.msl",
            "<image><read filename=\"xc:red\"/></image>");

        Assert.Throws<MagickPolicyErrorException>(() =>
        {
            using var blocked = new MagickImage();
            blocked.Read(msl, new MagickReadSettings { Format = MagickFormat.Mng });
        });
    }

}
