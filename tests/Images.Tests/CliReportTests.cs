using Images.Services;

namespace Images.Tests;

public sealed class CliReportTests
{
    [Fact]
    public void TryResolveMode_SystemInfo_ReturnsSystemInfo()
    {
        var mode = CliReport.TryResolveMode(["--system-info"]);
        Assert.Equal(CliMode.SystemInfo, mode);
    }

    [Fact]
    public void TryResolveMode_CodecReport_ReturnsCodecReport()
    {
        var mode = CliReport.TryResolveMode(["--codec-report"]);
        Assert.Equal(CliMode.CodecReport, mode);
    }

    [Fact]
    public void TryResolveMode_Version_ReturnsVersion()
    {
        var mode = CliReport.TryResolveMode(["--version"]);
        Assert.Equal(CliMode.Version, mode);
    }

    [Fact]
    public void TryResolveMode_Help_ReturnsHelp()
    {
        Assert.Equal(CliMode.Help, CliReport.TryResolveMode(["--help"]));
        Assert.Equal(CliMode.Help, CliReport.TryResolveMode(["-h"]));
        Assert.Equal(CliMode.Help, CliReport.TryResolveMode(["/?"]) );
    }

    [Fact]
    public void TryResolveMode_MultipleArgs_ReturnsNull()
    {
        var mode = CliReport.TryResolveMode(["--system-info", "extra"]);
        Assert.Null(mode);
    }

    [Fact]
    public void TryResolveMode_EmptyArgs_ReturnsNull()
    {
        var mode = CliReport.TryResolveMode([]);
        Assert.Null(mode);
    }

    [Fact]
    public void TryResolveMode_UnknownFlag_ReturnsNull()
    {
        var mode = CliReport.TryResolveMode(["--unknown"]);
        Assert.Null(mode);
    }

    [Fact]
    public void TryResolveMode_PerfReport_ReturnsPerfReport()
    {
        var mode = CliReport.TryResolveMode(["--perf-report"]);
        Assert.Equal(CliMode.PerfReport, mode);
    }

    [Fact]
    public void TryResolveMode_IsCaseInsensitive()
    {
        Assert.Equal(CliMode.SystemInfo, CliReport.TryResolveMode(["--SYSTEM-INFO"]));
        Assert.Equal(CliMode.CodecReport, CliReport.TryResolveMode(["--Codec-Report"]));
    }

    [Fact]
    public void BuildSystemInfo_IncludesMagickSecurityPolicy()
    {
        var report = CliReport.BuildSystemInfo();

        Assert.Contains("Magick policy:", report, StringComparison.Ordinal);
        Assert.Contains("Magick limits:", report, StringComparison.Ordinal);
        Assert.Contains("Magick blocked:", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHelpText_IncludesWindowFreeFaceDetectionCommand()
    {
        var help = CliReport.BuildHelpText();

        Assert.Contains("--face-detect <imagePath>", help, StringComparison.Ordinal);
        Assert.Contains("--face-xmp <imagePath>", help, StringComparison.Ordinal);
        Assert.Contains("--face-cluster <imagePath> <imagePath> [...]", help, StringComparison.Ordinal);
        Assert.Contains("--object-detect <imagePath>", help, StringComparison.Ordinal);
        Assert.Contains("--object-xmp <imagePath>", help, StringComparison.Ordinal);
        Assert.Contains("--orientation-suggest <imagePath>", help, StringComparison.Ordinal);
        Assert.Contains("--aesthetic-score <imagePath> [imagePath ...]", help, StringComparison.Ordinal);
        Assert.Contains("never modifies the image or sidecar", help, StringComparison.Ordinal);
    }
}
