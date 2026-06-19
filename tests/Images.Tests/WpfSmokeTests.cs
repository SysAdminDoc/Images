using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;

namespace Images.Tests;

[Trait("Category", "Smoke")]
public sealed class WpfSmokeTests : IDisposable
{
    private static readonly string FixtureImage = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "smoke-test.png"));

    private static readonly string AppExePath = FindAppExe();

    private Application? _app;
    private UIA3Automation? _automation;

    private static string FindAppExe()
    {
        var solutionDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exePath = Path.Combine(solutionDir,
            "src", "Images", "bin", "Release",
            "net10.0-windows10.0.22621.0", "Images.exe");
        return exePath;
    }

    private (Application App, Window MainWindow) LaunchApp(string? imagePath = null)
    {
        if (!File.Exists(AppExePath))
            throw new FileNotFoundException(
                $"App executable not found at {AppExePath}. Build in Release first.");

        var args = imagePath is not null ? $"\"{imagePath}\"" : "";
        _automation = new UIA3Automation();
        _app = Application.Launch(AppExePath, args);
        var mainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
        Assert.NotNull(mainWindow);
        return (_app, mainWindow);
    }

    private static void SkipUnlessSmoke()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_SMOKE_TESTS")))
            Assert.Skip("Set RUN_SMOKE_TESTS=1 to run WPF smoke tests (requires display session).");
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void LaunchAndClose()
    {
        SkipUnlessSmoke();
        var (app, window) = LaunchApp();
        Assert.Contains("Images", window.Title);
        app.Close();
        Assert.True(app.HasExited);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void OpenFixtureImage()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        Assert.Contains("smoke-test", window.Title, StringComparison.OrdinalIgnoreCase);

        var imageElement = window.FindFirstDescendant(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Image));
        Assert.NotNull(imageElement);

        app.Close();
    }

    [Fact]
    public void NavigateNextPrevious()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);

        window.Focus();
        System.Threading.Thread.Sleep(500);

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT);
        System.Threading.Thread.Sleep(300);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.LEFT);
        System.Threading.Thread.Sleep(300);

        Assert.False(app.HasExited);
        app.Close();
    }

    [Fact]
    public void EscapeClosesApp()
    {
        SkipUnlessSmoke();
        var (app, window) = LaunchApp();
        window.Focus();
        System.Threading.Thread.Sleep(500);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(3));
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void CanvasHasDocumentedAutomationNameAndHelpText()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1500);

        var canvas = window.FindFirstDescendant(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Image));
        Assert.NotNull(canvas);

        var name = canvas.Name;
        Assert.NotNull(name);
        Assert.Matches(@"Image, \d+ by \d+ pixels", name);

        var help = canvas.Properties.HelpText.ValueOrDefault;
        Assert.NotNull(help);
        Assert.Contains("arrow keys", help, StringComparison.OrdinalIgnoreCase);

        app.Close();
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void WindowTitleContainsFilenameWhenImageLoaded()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1000);

        Assert.Contains("smoke-test", window.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Images", window.Title, StringComparison.OrdinalIgnoreCase);

        app.Close();
    }

    [Fact]
    public void NavigationButtonsHaveAutomationNames()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1000);

        var prev = window.FindFirstDescendant(cf => cf.ByName("Previous image"));
        var next = window.FindFirstDescendant(cf => cf.ByName("Next image"));
        Assert.NotNull(prev);
        Assert.NotNull(next);

        app.Close();
    }

    [Fact]
    public void ToolbarButtonsHaveAutomationNames()
    {
        SkipUnlessSmoke();
        var (app, window) = LaunchApp();
        System.Threading.Thread.Sleep(1000);

        var openBtn = window.FindFirstDescendant(cf => cf.ByName("Open image"));
        Assert.NotNull(openBtn);

        var settingsBtn = window.FindFirstDescendant(cf => cf.ByName("Open settings"));
        Assert.NotNull(settingsBtn);

        var diagBtn = window.FindFirstDescendant(cf => cf.ByName("Open diagnostics"));
        Assert.NotNull(diagBtn);

        app.Close();
    }

    [Fact]
    public void FolderPositionChipExistsWhenImageLoaded()
    {
        SkipUnlessSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1500);

        var posChip = window.FindFirstDescendant(cf => cf.ByName("Folder position"));
        Assert.NotNull(posChip);

        app.Close();
    }

    public void Dispose()
    {
        try { _app?.Close(); } catch { }
        try { _app?.Dispose(); } catch { }
        _automation?.Dispose();
    }
}
