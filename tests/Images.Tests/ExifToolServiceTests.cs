using System.Diagnostics;
using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ExifToolServiceTests
{
    [Fact]
    public void Run_WritesUtf8ArgFileAndUsesArgumentList()
    {
        using var temp = TestDirectory.Create();
        var executable = temp.WriteFile("exiftool.exe", "stub");
        var target = temp.WriteFile("photo with spaces.jpg", "image");
        ProcessStartInfo? capturedStartInfo = null;
        string? capturedArgFile = null;

        var result = ExifToolService.Run(
            executable,
            ["-overwrite_original", "-XMP-dc:Subject+=portrait"],
            [target],
            temp.Path,
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                capturedArgFile = startInfo.ArgumentList[1];
                var lines = File.ReadAllLines(capturedArgFile);
                Assert.Equal(
                    [
                        // Non-ANSI filenames on Windows require this prefix, or
                        // ExifTool interprets paths in the system code page.
                        "-charset",
                        "filename=UTF8",
                        "-overwrite_original",
                        "-XMP-dc:Subject+=portrait",
                        Path.GetFullPath(target)
                    ],
                    lines);
                return new ExifToolProcessResult(0, "ok", "");
            });

        Assert.True(result.Succeeded);
        var start = Assert.IsType<ProcessStartInfo>(capturedStartInfo);
        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Equal(Path.GetFullPath(executable), start.FileName);
        var argFile = Assert.IsType<string>(capturedArgFile);
        Assert.Equal(["-@", argFile], start.ArgumentList);
        Assert.False(File.Exists(argFile));
    }

    [Theory]
    [InlineData("bad\rname.jpg")]
    [InlineData("bad\nname.jpg")]
    [InlineData("bad<name>.jpg")]
    [InlineData("bad>name.jpg")]
    [InlineData("bad|name.jpg")]
    public void Run_RejectsUnsafeTargetPathChannel(string unsafePath)
    {
        using var temp = TestDirectory.Create();
        var executable = temp.WriteFile("exiftool.exe", "stub");

        var result = ExifToolService.Run(
            executable,
            ["-XMP-dc:Subject+=portrait"],
            [Path.Combine(temp.Path, unsafePath)],
            temp.Path,
            (_, _) => throw new InvalidOperationException("Process should not run."));

        Assert.False(result.Succeeded);
        Assert.Contains("Unsafe ExifTool target path", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RejectsArgfileLineBreaksBeforeProcessStart()
    {
        using var temp = TestDirectory.Create();
        var executable = temp.WriteFile("exiftool.exe", "stub");
        var target = temp.WriteFile("photo.jpg", "image");

        var result = ExifToolService.Run(
            executable,
            ["-XMP-dc:Subject+=portrait\r\n-overwrite_original"],
            [target],
            temp.Path,
            (_, _) => throw new InvalidOperationException("Process should not run."));

        Assert.False(result.Succeeded);
        Assert.Contains("Unsafe ExifTool argument", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildStartInfo_NeverUsesShell()
    {
        var startInfo = ExifToolService.BuildStartInfo(@"C:\Tools\exiftool.exe", @"C:\Temp\args.txt");

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal(["-@", @"C:\Temp\args.txt"], startInfo.ArgumentList);
    }

    [Fact]
    public void RunProcess_DrainsLargeOutputWithoutTimingOut()
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.True(File.Exists(powerShell), "Windows PowerShell is required for this process-drain regression.");

        var startInfo = new ProcessStartInfo
        {
            FileName = powerShell,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("$chunk = 'x' * 1024; for ($i = 0; $i -lt 96; $i++) { [Console]::Out.WriteLine($chunk) }; [Console]::Error.WriteLine('stderr-ok')");

        var result = ExifToolService.RunProcess(startInfo, timeoutMilliseconds: 10000);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.StandardOutput.Length > 96 * 1024);
        Assert.Contains("stderr-ok", result.StandardError, StringComparison.Ordinal);
    }
}
