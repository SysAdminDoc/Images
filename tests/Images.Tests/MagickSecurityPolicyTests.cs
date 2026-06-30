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
        Assert.False(MagickSecurityPolicy.IsWriteFormatAllowed(MagickFormat.Pdf));
        Assert.False(MagickSecurityPolicy.IsWriteFormatAllowed(MagickFormat.Pdfa));
        Assert.False(MagickSecurityPolicy.IsWriteFormatAllowed(MagickFormat.Eps));
        Assert.False(MagickSecurityPolicy.IsWriteFormatAllowed(MagickFormat.Svg));
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

        Assert.Contains("Ghostscript", report.DocumentDelegateStatus, StringComparison.Ordinal);
        Assert.Contains(".pdf", report.BlockedWriteSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("memory", report.ResourceLimitSummary, StringComparison.OrdinalIgnoreCase);
    }
}
