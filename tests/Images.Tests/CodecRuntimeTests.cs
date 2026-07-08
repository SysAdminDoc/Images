using System.Diagnostics;
using Images.Services;

namespace Images.Tests;

public sealed class CodecRuntimeTests
{
    [Fact]
    public void Status_IsNotNull()
    {
        var status = CodecRuntime.Status;

        Assert.NotNull(status);
    }

    [Fact]
    public void Configure_ReturnsCodecRuntimeStatus()
    {
        var status = CodecRuntime.Configure();

        Assert.NotNull(status);
        Assert.IsType<CodecRuntimeStatus>(status);
    }

    [Fact]
    public void Status_HasNonNullMagickStatus()
    {
        var status = CodecRuntime.Status;

        Assert.NotNull(status.MagickStatus);
        Assert.NotEmpty(status.MagickStatus);
    }

    [Fact]
    public void Status_HasNonNullDocumentStatus()
    {
        var status = CodecRuntime.Status;

        Assert.NotNull(status.DocumentStatus);
        Assert.NotEmpty(status.DocumentStatus);
    }

    [Fact]
    public void RunVersionProbe_DrainsStderrWhileWaiting()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("$payload = 'x' * 200000; [Console]::Error.Write($payload); [Console]::Out.Write('10.02.1')");

        var version = CodecRuntime.RunVersionProbe(psi, timeoutMs: 5000);

        Assert.Equal("10.02.1", version);
    }
}
