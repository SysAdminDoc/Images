using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Images.Services;

namespace Images.Tests;

[Collection("WpfSmoke")]
[Trait("Category", "Smoke")]
public sealed class WpfSmokeTests : IDisposable
{
    private static readonly string FixtureImage = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "smoke-test.png"));

    private static readonly string AppExePath = FindAppExe();

    private Application? _app;
    private UIA3Automation? _automation;
    private readonly string _isolatedDataRoot = Path.Combine(
        Path.GetTempPath(),
        "Images-WpfSmoke",
        Guid.NewGuid().ToString("N"));
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;
    private const int GwlExStyle = -20;

    private static string FindAppExe()
    {
        var solutionDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exePath = Path.Combine(solutionDir,
            "src", "Images", "bin", "Release",
            "net10.0-windows10.0.22621.0", "Images.exe");
        return exePath;
    }

    private (Application App, Window MainWindow) LaunchApp(
        string? imagePath = null,
        bool background = true)
    {
        if (!File.Exists(AppExePath))
            throw new FileNotFoundException(
                $"App executable not found at {AppExePath}. Build in Release first.");

        Directory.CreateDirectory(_isolatedDataRoot);
        var startInfo = new ProcessStartInfo
        {
            FileName = AppExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (background)
            startInfo.ArgumentList.Add("--uia-background");
        if (imagePath is not null)
            startInfo.ArgumentList.Add(imagePath);
        startInfo.Environment["IMAGES_DATA_ROOT"] = _isolatedDataRoot;

        _automation = new UIA3Automation();
        _app = Application.Launch(startInfo);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        Window? mainWindow = null;
        while (mainWindow is null && DateTime.UtcNow < deadline)
        {
            mainWindow = _app.GetAllTopLevelWindows(_automation).FirstOrDefault();
            if (mainWindow is null)
                System.Threading.Thread.Sleep(100);
        }
        Assert.NotNull(mainWindow);
        if (background)
        {
            var windowHandle = new IntPtr(mainWindow.Properties.NativeWindowHandle.ValueOrDefault);
            Assert.NotEqual(IntPtr.Zero, windowHandle);
            Assert.NotEqual(windowHandle, GetForegroundWindow());
            Assert.True(OverlayWindowService.IsBackgroundSmokeStyle(
                GetWindowLong(windowHandle, GwlExStyle)));
        }
        return (_app, mainWindow);
    }

    private static void CloseApp(Application app, Window window)
    {
        window.Close();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!app.HasExited && DateTime.UtcNow < deadline)
            System.Threading.Thread.Sleep(50);
        Assert.True(app.HasExited);
    }

    private string CreateSmokeFolder(int imageCount)
    {
        var folder = Path.Combine(_isolatedDataRoot, "fixtures");
        Directory.CreateDirectory(folder);
        for (var i = 0; i < imageCount; i++)
            File.Copy(FixtureImage, Path.Combine(folder, $"smoke-{i + 1}.png"), overwrite: true);
        return folder;
    }

    private static void SkipUnlessBackgroundSmoke()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_BACKGROUND_SMOKE_TESTS")))
            Assert.Skip("Set RUN_BACKGROUND_SMOKE_TESTS=1 to run offscreen WPF smoke tests.");
    }

    private static void SkipUnlessInteractiveSmoke()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_SMOKE_TESTS")))
            Assert.Skip("Set RUN_SMOKE_TESTS=1 to run foreground-only input smoke tests.");
    }

    private static void SkipUnlessAnySmoke()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_BACKGROUND_SMOKE_TESTS")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RUN_SMOKE_TESTS")))
        {
            Assert.Skip("Set RUN_BACKGROUND_SMOKE_TESTS=1 or RUN_SMOKE_TESTS=1 to run WPF smoke tests.");
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    private static void ResizeWindow(Application app, int width, int height)
    {
        var handle = app.MainWindowHandle;
        Assert.NotEqual(IntPtr.Zero, handle);

        if (!SetWindowPos(handle, IntPtr.Zero, 40, 40, width, height, SwpNoZOrder | SwpShowWindow))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        System.Threading.Thread.Sleep(300);
    }

    private static AutomationElement WaitForElement(Func<AutomationElement?> find, string description)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        AutomationElement? element;
        while ((element = find()) is null)
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Timed out waiting for {description}.");

            System.Threading.Thread.Sleep(100);
        }

        return element;
    }

    private static AutomationElement? FindImageCanvas(Window window)
        => window.FindFirstDescendant(cf => cf.ByAutomationId("ImageCanvas"))
           ?? window
               .FindAllDescendants(cf => cf.ByControlType(ControlType.Image))
               .FirstOrDefault(element => element.Name.StartsWith("Image, ", StringComparison.Ordinal));

    [Fact]
    [Trait("Category", "SmokeGate")]
    [Trait("Category", "BackgroundSmoke")]
    public void LaunchAndClose()
    {
        SkipUnlessBackgroundSmoke();
        var (app, window) = LaunchApp();
        Assert.Contains("Images", window.Title);
        CloseApp(app, window);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    [Trait("Category", "BackgroundSmoke")]
    public void OpenFixtureImage()
    {
        SkipUnlessBackgroundSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        Assert.Contains("smoke-test", window.Title, StringComparison.OrdinalIgnoreCase);

        var imageElement = FindImageCanvas(window);
        Assert.NotNull(imageElement);

        CloseApp(app, window);
    }

    [Fact]
    public void NavigateNextPrevious()
    {
        SkipUnlessAnySmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);

        var next = WaitForElement(
            () => window.FindFirstDescendant(cf => cf.ByName("Next image")),
            "Next image button").AsButton();
        var previous = WaitForElement(
            () => window.FindFirstDescendant(cf => cf.ByName("Previous image")),
            "Previous image button").AsButton();
        next.Invoke();
        previous.Invoke();

        Assert.False(app.HasExited);
        CloseApp(app, window);
    }

    [Fact]
    public void EscapeClosesApp()
    {
        SkipUnlessInteractiveSmoke();
        var (app, window) = LaunchApp(background: false);
        window.Focus();
        System.Threading.Thread.Sleep(500);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(3));
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    [Trait("Category", "BackgroundSmoke")]
    public void CanvasHasDocumentedAutomationNameAndHelpText()
    {
        SkipUnlessBackgroundSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1500);

        var canvas = FindImageCanvas(window);
        Assert.NotNull(canvas);

        var name = canvas.Name;
        Assert.NotNull(name);
        Assert.Matches(@"Image, \d+ by \d+ pixels", name);

        var help = canvas.Properties.HelpText.ValueOrDefault;
        Assert.NotNull(help);
        Assert.Contains("arrow keys", help, StringComparison.OrdinalIgnoreCase);

        CloseApp(app, window);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    [Trait("Category", "BackgroundSmoke")]
    public void WindowTitleContainsFilenameWhenImageLoaded()
    {
        SkipUnlessBackgroundSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1000);

        Assert.Contains("smoke-test", window.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Images", window.Title, StringComparison.OrdinalIgnoreCase);

        CloseApp(app, window);
    }

    [Fact]
    [Trait("Category", "BackgroundSmoke")]
    public void NavigationButtonsHaveAutomationNames()
    {
        SkipUnlessBackgroundSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage);
        System.Threading.Thread.Sleep(1000);

        var prev = window.FindFirstDescendant(cf => cf.ByName("Previous image"));
        var next = window.FindFirstDescendant(cf => cf.ByName("Next image"));
        Assert.NotNull(prev);
        Assert.NotNull(next);

        CloseApp(app, window);
    }

    [Fact]
    public void ToolbarButtonsHaveAutomationNames()
    {
        SkipUnlessAnySmoke();
        var (app, window) = LaunchApp();
        System.Threading.Thread.Sleep(1000);

        var openBtn = window.FindFirstDescendant(cf => cf.ByName("Open image"));
        Assert.NotNull(openBtn);

        var settingsBtn = window.FindFirstDescendant(cf => cf.ByName("Open settings"));
        Assert.NotNull(settingsBtn);

        var diagBtn = window.FindFirstDescendant(cf => cf.ByName("Open diagnostics"));
        Assert.NotNull(diagBtn);

        CloseApp(app, window);
    }

    [Fact]
    public void FolderPositionChipExistsWhenImageLoaded()
    {
        SkipUnlessAnySmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var folder = CreateSmokeFolder(2);
        var (app, window) = LaunchApp(Path.Combine(folder, "smoke-1.png"));
        System.Threading.Thread.Sleep(1500);

        var posChip = window.FindFirstDescendant(cf => cf.ByName("Folder position"));
        Assert.NotNull(posChip);

        CloseApp(app, window);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    [Trait("Category", "BackgroundSmoke")]
    public void FolderFilmstripContainsFixtureImage()
    {
        SkipUnlessBackgroundSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var folder = CreateSmokeFolder(2);
        var (app, window) = LaunchApp(Path.Combine(folder, "smoke-1.png"));
        var filmstrip = WaitForElement(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("FolderFilmstrip")),
            "folder filmstrip");
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        AutomationElement[] items;
        do
        {
            items = filmstrip.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            if (items.Length > 0)
                break;
            System.Threading.Thread.Sleep(100);
        } while (DateTime.UtcNow < deadline);

        Assert.Equal(2, items.Length);
        CloseApp(app, window);
    }

    [Fact]
    [Trait("Category", "SmokeGate")]
    public void ViewportContextMenuIsBoundedAndKeyboardReachable()
    {
        SkipUnlessInteractiveSmoke();
        if (!File.Exists(FixtureImage))
            throw new FileNotFoundException($"Fixture image missing: {FixtureImage}");

        var (app, window) = LaunchApp(FixtureImage, background: false);
        ResizeWindow(app, width: 760, height: 560);
        window.SetForeground();
        System.Threading.Thread.Sleep(1000);

        var canvas = FindImageCanvas(window);
        Assert.NotNull(canvas);

        var canvasBounds = canvas.BoundingRectangle;
        Mouse.RightClick(new System.Drawing.Point(
            canvasBounds.Left + canvasBounds.Width / 2,
            canvasBounds.Top + canvasBounds.Height / 2));
        var menu = WaitForElement(
            () => window.ContextMenu ?? _automation!.GetDesktop().FindFirstDescendant(cf => cf.ByName("Viewport context menu")),
            "viewport context menu");

        var rootItemNames = menu
            .FindAllChildren(cf => cf.ByControlType(ControlType.MenuItem))
            .Select(item => item.Name)
            .ToArray();

        Assert.Contains(rootItemNames, name => name.StartsWith("Open", StringComparison.Ordinal));
        Assert.Contains("Compare", rootItemNames);
        Assert.Contains("View", rootItemNames);
        Assert.Contains("Edit", rootItemNames);
        Assert.Contains("Transform", rootItemNames);
        Assert.Contains("Organize", rootItemNames);
        Assert.Contains("Workflow tools", rootItemNames);
        Assert.Contains("File actions", rootItemNames);
        Assert.Contains("Output", rootItemNames);

        var menuBounds = menu.BoundingRectangle;
        var windowBounds = window.BoundingRectangle;
        var verticalScrollBar = menu.FindFirstDescendant(cf => cf.ByControlType(ControlType.ScrollBar));
        Assert.True(
            menuBounds.Height <= windowBounds.Height || verticalScrollBar is not null,
            $"Viewport context menu height {menuBounds.Height} exceeded constrained window height {windowBounds.Height} without a scrollbar.");

        var compare = WaitForElement(
            () => menu.FindFirstDescendant(cf => cf.ByName("Compare")),
            "Compare root menu item");
        compare.Focus();
        Keyboard.Press(VirtualKeyShort.RIGHT);

        var compareWith = WaitForElement(
            () => _automation!.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId("ViewportContextMenuCompareWith"))
                ?? _automation!.GetDesktop().FindFirstDescendant(cf => cf.ByName("Compare with…")),
            "Compare with submenu item");
        Assert.False(compareWith.IsOffscreen);

        Keyboard.Press(VirtualKeyShort.ESCAPE);
        CloseApp(app, window);
    }

    public void Dispose()
    {
        try
        {
            if (_app is { HasExited: false })
                _app.Kill();
        }
        catch { }
        try { _app?.Dispose(); } catch { }
        _automation?.Dispose();
        try
        {
            if (Directory.Exists(_isolatedDataRoot))
                Directory.Delete(_isolatedDataRoot, recursive: true);
        }
        catch { }
    }
}
