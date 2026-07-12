using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Images.Controls;
using Images.Services;
using Images.ViewModels;

namespace Images;

public partial class MainWindow : Window
{
    private const double WorkAreaMargin = 24;

    internal enum DropOpenKind
    {
        None,
        Folder,
        SingleFile,
        FileList
    }

    internal sealed record DropOpenRequest(DropOpenKind Kind, IReadOnlyList<string> Paths)
    {
        public static DropOpenRequest None { get; } = new(DropOpenKind.None, []);
        public bool IsAccepted => Kind != DropOpenKind.None;
        public string? FirstPath => Paths.Count > 0 ? Paths[0] : null;
    }

    private MainViewModel Vm => (MainViewModel)DataContext;
    private PixelCoordinate? _inspectorSelectionStart;
    private PixelCoordinate? _canvasSelectionStart;
    private PixelCoordinate? _cropSelectionStart;
    private bool _exposureBrushPainting;
    private bool _redEyeCorrectionPainting;
    private bool _retouchPainting;
    private HwndSource? _hwndSource;
    private bool _overlayExitHotKeyRegistered;
    private bool _syncingCompareCanvases;
    private string? _dragPathVerdictKey;
    private string? _dragPathVerdict;

    public MainWindow()
    {
        InitializeComponent();

        // V20-02: window-state persistence. Restore BEFORE Show so WPF positions first-paint
        // correctly; save on Closing.
        RestoreWindowState();
        ClampWindowToWorkArea();
        Closing += SaveWindowState;

        // V20-27: wire the send-to-monitor callback and rebuild the palette so dynamic
        // "Send to monitor N" entries get a live delegate.
        Vm.RequestSendToMonitor = SendToMonitor;
        Vm.RequestArchivePassword = PromptArchivePassword;
        Vm.RefreshCommandPalette();
        Closed += (_, _) =>
        {
            UnregisterOverlayExitHotKey();
            _hwndSource?.RemoveHook(WndProc);
            Vm.Dispose();
        };
        Vm.FolderPreviewItems.CollectionChanged += (_, _) => QueueCenterCurrentPreviewItems();
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ShowFilmstrip) or nameof(MainViewModel.ShowSideFolderPreview))
            {
                QueueCenterCurrentPreviewItems();
            }
            if (e.PropertyName is nameof(MainViewModel.ShowGallery))
            {
                QueueCenterCurrentPreviewItems();
            }
            if (e.PropertyName is nameof(MainViewModel.IsPinnedOverlayMode)
                or nameof(MainViewModel.IsOverlayClickThrough)
                or nameof(MainViewModel.OverlayOpacity))
            {
                ApplyOverlayWindowState();
            }
            if (e.PropertyName is nameof(MainViewModel.MotionVideoPath))
            {
                if (Vm.MotionVideoPath is not null)
                    MotionVideoPlayer.Source = new Uri(Vm.MotionVideoPath, UriKind.Absolute);
                else
                {
                    MotionVideoPlayer.Stop();
                    MotionVideoPlayer.Source = null;
                }
            }
        };

        Canvas.SwipeNavigate += (_, dir) =>
        {
            if (dir == Controls.SwipeDirection.Left) Vm.NextCommand.Execute(null);
            else Vm.PrevCommand.Execute(null);
        };
        Viewport.MouseEnter += (_, _) => FadeArrows(1.0);
        Viewport.MouseLeave += (_, _) => FadeArrows(0.0);
        _edgeHideTimer.Tick += EdgeHideTimer_Tick;
        MouseMove += (_, e) => FullscreenEdgeCheck(e.GetPosition(this));
        Loaded += (_, _) =>
        {
            Focus();
            QueueCenterCurrentPreviewItems();
            if (Vm.IsPeekMode) return;

            // P-04: kick off a throttled update check 3 seconds after UI is interactive so the
            // first image load isn't competing with HTTPS handshake. Fire-and-forget; any
            // failure is swallowed inside CheckForUpdatesAsync.
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000).ConfigureAwait(false);
                await Dispatcher.InvokeAsync(async () => await Vm.CheckForUpdatesAsync(userInitiated: false));
            });
        };
        SourceInitialized += OnSourceInitialized;
    }

    private void Viewport_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Viewport.ContextMenu is not { } menu) return;

        menu.PlacementTarget = Viewport;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private bool _previewCenterQueued;

    private void QueueCenterCurrentPreviewItems()
    {
        if (_previewCenterQueued) return;

        _previewCenterQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _previewCenterQueued = false;
            CenterCurrentPreviewItems();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CenterCurrentPreviewItems()
    {
        CenterCurrentPreviewItem(FilmstripItems);
        CenterCurrentPreviewItem(SidePreviewItems);
        CenterCurrentPreviewItem(GalleryItems);
    }

    private static void CenterCurrentPreviewItem(ListBox items)
    {
        if (!items.IsVisible || items.Items.Count == 0) return;

        FolderPreviewItem? current = null;
        foreach (var item in items.Items)
        {
            if (item is FolderPreviewItem previewItem && previewItem.IsCurrent)
            {
                current = previewItem;
                break;
            }
        }

        if (current is null) return;
        items.ScrollIntoView(current);
        items.UpdateLayout();

        var scroll = FindVisualChild<ScrollViewer>(items);
        if (scroll is null) return;

        scroll.UpdateLayout();
        if (scroll.ViewportWidth <= 0) return;
        if (items.ItemContainerGenerator.ContainerFromItem(current) is not FrameworkElement container) return;
        if (container.RenderSize.Width <= 0) return;

        Rect bounds;
        try
        {
            bounds = container.TransformToAncestor(scroll)
                .TransformBounds(new Rect(new Point(0, 0), container.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var target = scroll.HorizontalOffset + bounds.Left + bounds.Width / 2 - scroll.ViewportWidth / 2;
        if (double.IsNaN(target) || double.IsInfinity(target)) return;

        scroll.ScrollToHorizontalOffset(Math.Clamp(target, 0, scroll.ScrollableWidth));
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }

        return null;
    }

    private void PreviewThumbnail_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderPreviewItem item })
            Vm.EnsurePreviewThumbnailCommand.Execute(item);
    }

    private void GalleryItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsGalleryOpen && Vm.SelectedGalleryItem is not null)
            Vm.IsGalleryOpen = false;
    }

    private void FolderSortButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Flip the native title bar to dark so the Catppuccin Mocha interior doesn't sit inside a
        // default light caption. Best-effort — pre-20H1 no-ops cleanly via the service.
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowChrome.ApplyDarkCaption(hwnd);
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        ApplyOverlayWindowState();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == OverlayWindowService.WmHotKey && wParam.ToInt32() == OverlayWindowService.ExitHotKeyId)
        {
            Vm.ExitOverlayModeCommand.Execute(null);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ApplyOverlayWindowState()
    {
        if (Vm.IsPeekMode)
            return;

        Topmost = Vm.IsPinnedOverlayMode;
        Opacity = Vm.IsPinnedOverlayMode ? Vm.OverlayOpacity : 1.0;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var wantsClickThrough = Vm.IsPinnedOverlayMode && Vm.IsOverlayClickThrough;
        EnsureOverlayExitHotKey(hwnd, Vm.IsPinnedOverlayMode);
        OverlayWindowService.ApplyClickThrough(hwnd, wantsClickThrough && _overlayExitHotKeyRegistered);
        Vm.SetOverlayExitHotKeyAvailable(!Vm.IsPinnedOverlayMode || _overlayExitHotKeyRegistered);
    }

    private void EnsureOverlayExitHotKey(IntPtr hwnd, bool shouldRegister)
    {
        if (shouldRegister)
        {
            if (_overlayExitHotKeyRegistered)
                return;

            _overlayExitHotKeyRegistered = OverlayWindowService.RegisterExitHotKey(hwnd);
            return;
        }

        UnregisterOverlayExitHotKey();
    }

    private void UnregisterOverlayExitHotKey()
    {
        if (!_overlayExitHotKeyRegistered)
            return;

        var hwnd = new WindowInteropHelper(this).Handle;
        OverlayWindowService.UnregisterExitHotKey(hwnd);
        _overlayExitHotKeyRegistered = false;
    }

    public void OpenPath(string path) => Vm.OpenFile(path);

    public void OpenPathList(IReadOnlyList<string> paths) => Vm.OpenFileList(paths);

    /// <summary>
    /// V20-31: start listen mode on the specified loopback TCP port.
    /// Called by App.xaml.cs when the launch argv contains <c>--listen &lt;port&gt;</c>.
    /// </summary>
    public void StartListenMode(int port) => Vm.StartListenMode(port);

    /// <summary>
    /// V20-32: enter chromeless preview mode (PowerToys-Peek-style invocation). The window
    /// becomes a borderless, topmost, maximized overlay with the side panel + bottom toolbar
    /// hidden — only the image and floating navigation arrows remain. Escape closes the window.
    /// Called by App.xaml.cs when the launch argv matches `--peek &lt;path&gt;`.
    /// </summary>
    public void EnterPeekMode(string path)
    {
        Vm.IsPeekMode = true;
        Vm.IsFullscreen = true;          // collapses the side-panel column to 0 width
        WindowStyle = WindowStyle.None;  // borderless
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        Topmost = true;                  // floats over whatever invoked us
        Vm.OpenFile(path);
    }

    private static readonly CubicEase _easeOut = new() { EasingMode = EasingMode.EaseOut };

    private void FadeArrows(double target)
    {
        if (!Vm.HasImage) return;
        if (SettingsService.Instance.GetBool(Keys.AccessibilityReduceMotion, false))
        {
            PrevArrow.Opacity = target;
            NextArrow.Opacity = target;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = _easeOut,
        };
        PrevArrow.BeginAnimation(OpacityProperty, anim);
        NextArrow.BeginAnimation(OpacityProperty, anim);
    }

    // V20-20: advance the zoom-mode wheel Fit → 1:1 → FitWidth → FitHeight → Fill → Fit.
    private Images.Controls.ZoomPanImage.ZoomMode _zoomMode = Images.Controls.ZoomPanImage.ZoomMode.Fit;
    private Images.Controls.ZoomPanImage.ZoomMode NextZoomMode()
    {
        _zoomMode = _zoomMode switch
        {
            Images.Controls.ZoomPanImage.ZoomMode.Fit       => Images.Controls.ZoomPanImage.ZoomMode.OneToOne,
            Images.Controls.ZoomPanImage.ZoomMode.OneToOne  => Images.Controls.ZoomPanImage.ZoomMode.FitWidth,
            Images.Controls.ZoomPanImage.ZoomMode.FitWidth  => Images.Controls.ZoomPanImage.ZoomMode.FitHeight,
            Images.Controls.ZoomPanImage.ZoomMode.FitHeight => Images.Controls.ZoomPanImage.ZoomMode.Fill,
            _                                                => Images.Controls.ZoomPanImage.ZoomMode.Fit,
        };
        Vm.ShowToast($"Zoom: {_zoomMode}");
        return _zoomMode;
    }

    private void CompareCanvas_ViewChanged(object sender, EventArgs e)
    {
        if (_syncingCompareCanvases ||
            !Vm.ShowCompareMode ||
            sender is not ZoomPanImage source ||
            !source.IsVisible)
        {
            return;
        }

        _syncingCompareCanvases = true;
        try
        {
            var state = source.GetViewState();
            foreach (var target in VisibleCompareCanvases())
            {
                if (!ReferenceEquals(target, source))
                    target.SetViewState(state);
            }
        }
        finally
        {
            _syncingCompareCanvases = false;
        }
    }

    private IEnumerable<ZoomPanImage> VisibleCompareCanvases()
    {
        if (ComparePrimaryCanvas?.IsVisible == true)
            yield return ComparePrimaryCanvas;
        if (CompareSecondaryCanvas?.IsVisible == true)
            yield return CompareSecondaryCanvas;
        if (CompareOverlayPrimaryCanvas?.IsVisible == true)
            yield return CompareOverlayPrimaryCanvas;
        if (CompareOverlaySecondaryCanvas?.IsVisible == true)
            yield return CompareOverlaySecondaryCanvas;
    }

    private ZoomPanImage ActiveImageCanvas()
    {
        if (Vm.ShowCompareMode)
        {
            if (ComparePrimaryCanvas?.IsVisible == true)
                return ComparePrimaryCanvas;
            if (CompareOverlayPrimaryCanvas?.IsVisible == true)
                return CompareOverlayPrimaryCanvas;
        }

        return Canvas;
    }

    private void SetActiveZoomMode(ZoomPanImage.ZoomMode mode) => ActiveImageCanvas().SetZoomMode(mode);
    private void ZoomActiveCanvasBy(double factor) => ActiveImageCanvas().ZoomBy(factor);
    private void ResetActiveCanvas() => ActiveImageCanvas().ResetView();
    private void OneToOneActiveCanvas() => ActiveImageCanvas().OneToOne();

    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.HasDisplayImage) return;

        _zoomMode = ZoomPanImage.ZoomMode.Fit;
        SetActiveZoomMode(_zoomMode);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.HasDisplayImage)
            ZoomActiveCanvasBy(1 / 1.2);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.HasDisplayImage)
            ZoomActiveCanvasBy(1.2);
    }

    // V20-02 + V20-27: restore saved window geometry with per-monitor awareness. When the app
    // was last on monitor X, we try to restore that monitor's saved geometry. Falls back to the
    // generic (legacy) keys, then to the primary work area clamp so nothing lands offscreen.
    private void RestoreWindowState()
    {
        var settings = SettingsService.Instance;
        if (!settings.GetBool(Keys.RememberWindowPlacement, true)) return;

        // V20-27: try per-monitor keys first. On first launch (before SourceInitialized) the
        // hwnd is 0, so GetCurrentMonitorDeviceName returns null; that's fine — we just use the
        // generic fallback, which is the same behaviour as before V20-27.
        var monitorId = MonitorService.GetCurrentMonitorDeviceName(this);
        var suffix = monitorId is not null ? "." + MonitorService.SanitizeDeviceName(monitorId) : null;

        var (w, h, l, t, maximized) = LoadWindowGeometry(settings, suffix);

        // Sanity clamp: width/height must be at least MinWidth/MinHeight; position must be
        // at least partially on-screen (>= 120 px of the window visible on any work area).
        // V20-27: use the actual monitor work area (physical -> logical) instead of just primary.
        var wa = monitorId is not null
            ? MonitorService.GetCurrentMonitorWorkArea(this)
            : SystemParameters.WorkArea;

        // Convert physical work-area to logical units for clamping (only when we got real
        // monitor data — the fallback WorkArea is already in logical units).
        var waForClamp = monitorId is not null
            ? PhysicalToLogical(wa)
            : wa;

        if (w >= MinWidth && h >= MinHeight && w <= waForClamp.Width * 4 && h <= waForClamp.Height * 4)
        {
            Width = w; Height = h;
        }

        if (!double.IsNaN(l) && !double.IsNaN(t))
        {
            var visibleL = Math.Max(waForClamp.Left, Math.Min(l, waForClamp.Right - 120));
            var visibleT = Math.Max(waForClamp.Top, Math.Min(t, waForClamp.Bottom - 120));
            Left = visibleL; Top = visibleT;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (maximized) WindowState = WindowState.Maximized;
    }

    private void ClampWindowToWorkArea()
    {
        if (WindowState == WindowState.Maximized)
            return;

        var workArea = SystemParameters.WorkArea;
        if (workArea.Width <= 0 || workArea.Height <= 0)
            return;

        var maxWidth = Math.Max(MinWidth, workArea.Width - WorkAreaMargin);
        var maxHeight = Math.Max(MinHeight, workArea.Height - WorkAreaMargin);
        Width = Math.Clamp(Width, MinWidth, maxWidth);
        Height = Math.Clamp(Height, MinHeight, maxHeight);

        if (WindowStartupLocation != WindowStartupLocation.Manual)
            return;

        var maxLeft = Math.Max(workArea.Left, workArea.Right - Width);
        var maxTop = Math.Max(workArea.Top, workArea.Bottom - Height);
        Left = Math.Clamp(Left, workArea.Left, maxLeft);
        Top = Math.Clamp(Top, workArea.Top, maxTop);
    }

    /// <summary>
    /// Loads window geometry from per-monitor keys (if suffix is non-null), falling back to
    /// the generic (legacy) keys.
    /// </summary>
    private static (double w, double h, double l, double t, bool maximized) LoadWindowGeometry(
        SettingsService settings, string? monitorSuffix)
    {
        // Try monitor-specific keys first.
        if (monitorSuffix is not null)
        {
            var mw = settings.GetDouble(Keys.WindowWidth + monitorSuffix, double.NaN);
            if (!double.IsNaN(mw))
            {
                return (
                    mw,
                    settings.GetDouble(Keys.WindowHeight + monitorSuffix, 600),
                    settings.GetDouble(Keys.WindowLeft + monitorSuffix, double.NaN),
                    settings.GetDouble(Keys.WindowTop + monitorSuffix, double.NaN),
                    settings.GetBool(Keys.WindowMaximized + monitorSuffix, false));
            }
        }

        // Fall back to generic (legacy) keys.
        return (
            settings.GetDouble(Keys.WindowWidth, double.NaN),
            settings.GetDouble(Keys.WindowHeight, double.NaN),
            settings.GetDouble(Keys.WindowLeft, double.NaN),
            settings.GetDouble(Keys.WindowTop, double.NaN),
            settings.GetBool(Keys.WindowMaximized, false));
    }

    private void SaveWindowState(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Vm.IsPeekMode) return;

        var settings = SettingsService.Instance;
        if (!settings.GetBool(Keys.RememberWindowPlacement, true)) return;

        // Only record non-maximized geometry; if the user's maximized, the RestoreBounds holds
        // what they'd get back after unmaximize, so that's what we want to persist.
        var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);

        // V20-27: save both generic (legacy) keys and per-monitor keys.
        settings.SetDouble(Keys.WindowLeft, bounds.Left);
        settings.SetDouble(Keys.WindowTop, bounds.Top);
        settings.SetDouble(Keys.WindowWidth, bounds.Width);
        settings.SetDouble(Keys.WindowHeight, bounds.Height);
        settings.SetBool(Keys.WindowMaximized, WindowState == WindowState.Maximized);

        var monitorId = MonitorService.GetCurrentMonitorDeviceName(this);
        if (monitorId is not null)
        {
            var suffix = "." + MonitorService.SanitizeDeviceName(monitorId);
            settings.SetDouble(Keys.WindowLeft + suffix, bounds.Left);
            settings.SetDouble(Keys.WindowTop + suffix, bounds.Top);
            settings.SetDouble(Keys.WindowWidth + suffix, bounds.Width);
            settings.SetDouble(Keys.WindowHeight + suffix, bounds.Height);
            settings.SetBool(Keys.WindowMaximized + suffix, WindowState == WindowState.Maximized);
        }
    }

    /// <summary>
    /// V20-27: move the window to the monitor at the given zero-based index. Called from the
    /// VM via <see cref="MainViewModel.RequestSendToMonitor"/>.
    /// </summary>
    public void SendToMonitor(int monitorIndex)
    {
        var monitors = MonitorService.GetAllMonitors();
        if (monitorIndex < 0 || monitorIndex >= monitors.Count) return;
        MonitorService.MoveWindowToMonitor(this, monitors[monitorIndex]);
    }

    private string? PromptArchivePassword(string archiveName)
    {
        var dialog = new Window
        {
            Title = Localization.Strings.ArchivePasswordTitle,
            Width = 420,
            SizeToContent = System.Windows.SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Owner = this
        };
        dialog.SetResourceReference(Window.BackgroundProperty, "BaseBrush");

        var hwnd = default(IntPtr);
        dialog.SourceInitialized += (_, _) =>
        {
            hwnd = new System.Windows.Interop.WindowInteropHelper(dialog).Handle;
            Services.WindowChrome.ApplyDarkCaption(hwnd);
        };

        string? result = null;
        var passwordBox = new System.Windows.Controls.PasswordBox
        {
            FontSize = 14,
            Padding = new Thickness(8, 6, 8, 6)
        };
        System.Windows.Automation.AutomationProperties.SetName(
            passwordBox, Localization.Strings.ArchivePasswordTitle);

        var okButton = new System.Windows.Controls.Button
        {
            Content = Localization.Strings.Ok,
            IsDefault = true,
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0)
        };
        okButton.SetResourceReference(FrameworkElement.StyleProperty, "ChromeButton");
        okButton.Click += (_, _) => { result = passwordBox.Password; dialog.DialogResult = true; };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = Localization.Strings.Cancel,
            IsCancel = true,
            MinWidth = 80
        };
        cancelButton.SetResourceReference(FrameworkElement.StyleProperty, "ChromeButton");

        var buttonsPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(okButton);

        var labelText = new System.Windows.Controls.TextBlock
        {
            Text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                Localization.Strings.ArchivePasswordPrompt,
                archiveName),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 12)
        };
        labelText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");

        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(labelText);
        panel.Children.Add(passwordBox);
        panel.Children.Add(buttonsPanel);

        dialog.Content = panel;
        passwordBox.Focus();

        return dialog.ShowDialog() == true ? result : null;
    }

    /// <summary>
    /// Converts a physical-pixel rect to WPF logical units using the current DPI.
    /// </summary>
    private Rect PhysicalToLogical(Rect physical)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
            return physical;

        var dpiX = source.CompositionTarget.TransformFromDevice.M11;
        var dpiY = source.CompositionTarget.TransformFromDevice.M22;
        return new Rect(
            physical.Left * dpiX,
            physical.Top * dpiY,
            physical.Width * dpiX,
            physical.Height * dpiY);
    }

    // V15-07: F11 toggles fullscreen. Borderless maximized, side panel collapses via the
    // IsFullscreen VM flag. Previous window state is remembered so exit restores it exactly.
    private WindowState _preFullscreenState = WindowState.Normal;
    private WindowStyle _preFullscreenStyle = WindowStyle.SingleBorderWindow;
    private const double FullscreenEdgeZone = 40;
    private readonly System.Windows.Threading.DispatcherTimer _edgeHideTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };
    private bool _fullscreenToolbarRevealed;
    private bool _fullscreenSidePanelRevealed;

    private void ToggleFullscreen()
    {
        if (Vm.IsFullscreen)
        {
            WindowStyle = _preFullscreenStyle;
            WindowState = _preFullscreenState;
            ResizeMode = ResizeMode.CanResize;
            Vm.IsFullscreen = false;
            RestoreFullscreenPanels();
        }
        else
        {
            _preFullscreenState = WindowState;
            _preFullscreenStyle = WindowStyle;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Vm.IsFullscreen = true;
            HideFullscreenPanels();
        }
    }

    private void HideFullscreenPanels()
    {
        BottomToolbarGrid.Visibility = Visibility.Collapsed;
        RightSidePanel.Visibility = Visibility.Collapsed;
        _fullscreenToolbarRevealed = false;
        _fullscreenSidePanelRevealed = false;
    }

    private void RestoreFullscreenPanels()
    {
        _edgeHideTimer.Stop();
        BottomToolbarGrid.Visibility = Vm.IsPeekMode ? Visibility.Collapsed : Visibility.Visible;
        RightSidePanel.Visibility = Visibility.Visible;
        _fullscreenToolbarRevealed = false;
        _fullscreenSidePanelRevealed = false;
    }

    private void FullscreenEdgeCheck(Point mousePos)
    {
        if (!Vm.IsFullscreen || Vm.IsPeekMode)
            return;

        var height = ActualHeight;
        var width = ActualWidth;
        var nearBottom = mousePos.Y >= height - FullscreenEdgeZone;
        var nearRight = mousePos.X >= width - FullscreenEdgeZone;

        if (nearBottom && !_fullscreenToolbarRevealed)
        {
            BottomToolbarGrid.Visibility = Visibility.Visible;
            _fullscreenToolbarRevealed = true;
            RestartEdgeHideTimer();
        }
        else if (!nearBottom && _fullscreenToolbarRevealed)
        {
            BottomToolbarGrid.Visibility = Visibility.Collapsed;
            _fullscreenToolbarRevealed = false;
        }

        if (nearRight && !_fullscreenSidePanelRevealed)
        {
            RightSidePanel.Visibility = Visibility.Visible;
            _fullscreenSidePanelRevealed = true;
            RestartEdgeHideTimer();
        }
        else if (!nearRight && _fullscreenSidePanelRevealed)
        {
            RightSidePanel.Visibility = Visibility.Collapsed;
            _fullscreenSidePanelRevealed = false;
        }
    }

    private void RestartEdgeHideTimer()
    {
        _edgeHideTimer.Stop();
        _edgeHideTimer.Start();
    }

    private void EdgeHideTimer_Tick(object? sender, EventArgs e)
    {
        _edgeHideTimer.Stop();
        if (!Vm.IsFullscreen)
            return;

        var mousePos = Mouse.GetPosition(this);
        var nearBottom = mousePos.Y >= ActualHeight - FullscreenEdgeZone;
        var nearRight = mousePos.X >= ActualWidth - FullscreenEdgeZone;

        if (!nearBottom && _fullscreenToolbarRevealed)
        {
            BottomToolbarGrid.Visibility = Visibility.Collapsed;
            _fullscreenToolbarRevealed = false;
        }

        if (!nearRight && _fullscreenSidePanelRevealed)
        {
            RightSidePanel.Visibility = Visibility.Collapsed;
            _fullscreenSidePanelRevealed = false;
        }
    }

    // V15-01: 5-button mouse browsers have trained everyone that XButton1 = back / XButton2 =
    // forward. Hooking PreviewMouseDown at the window level catches the event before any
    // element captures it (e.g. drag-pan on ZoomPanImage). We gate on HasImage via the command
    // CanExecute; when no image is loaded nothing fires. LeftButton / RightButton / MiddleButton
    // are untouched so drag-pan + the V15-02 context menu coexist.
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Text entry controls should not be hijacked — rename-in-progress shouldn't eat XButton events.
        if (IsTextEntryElement(Keyboard.FocusedElement)) return;

        switch (e.ChangedButton)
        {
            case MouseButton.XButton1:
                if (Vm.PrevCommand.CanExecute(null))
                    Vm.PrevCommand.Execute(null);
                e.Handled = true;
                break;
            case MouseButton.XButton2:
                if (Vm.NextCommand.CanExecute(null))
                    Vm.NextCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var shortcutKey = GetShortcutKey(e);

        // V20-29: the command palette shortcut toggles even when its TextBox has focus.
        if (Vm.IsCommandShortcut(CommandIds.CommandPalette, shortcutKey, Keyboard.Modifiers))
        {
            ToggleCommandPaletteFocus();
            e.Handled = true;
            return;
        }

        if (shortcutKey != Key.Enter ||
            !Vm.IsCropMode ||
            IsTextEntryElement(Keyboard.FocusedElement) ||
            !Vm.ApplyCropCommand.CanExecute(null))
        {
            return;
        }

        Vm.ApplyCropCommand.Execute(null);
        Keyboard.ClearFocus();
        Focus();
        e.Handled = true;
    }

    private static Key GetShortcutKey(KeyEventArgs e)
        => e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key
        };

    internal static bool IsTextEntryElement(object? focusedElement)
        => focusedElement is TextBox
            or PasswordBox
            or RichTextBox
            or ComboBox { IsEditable: true };

    private void ToggleCommandPaletteFocus()
    {
        Vm.ShowCommandPalette = !Vm.ShowCommandPalette;
        if (Vm.ShowCommandPalette)
        {
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                () => CommandPaletteInput.Focus());
        }
        else
        {
            Keyboard.ClearFocus();
            Focus();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var shortcutKey = GetShortcutKey(e);

        // RD-08: Escape aborts an in-progress rubber-band zoom selection.
        if (e.Key == Key.Escape && Canvas.IsZoomSelecting)
        {
            Canvas.CancelZoomSelection();
            e.Handled = true;
            return;
        }

        // Don't steal keys from text entry controls.
        if (IsTextEntryElement(Keyboard.FocusedElement)) return;

        // V15-03: any key while the cheatsheet is open dismisses it and swallows the key so
        // the user doesn't trigger whatever shortcut they pressed by accident.
        if (Vm.ShowCheatsheet && e.Key != Key.OemQuestion)
        {
            Vm.ShowCheatsheet = false;
            e.Handled = true;
            return;
        }

        // V20-29: when the command palette is open but focus is NOT in its TextBox, dismiss
        // on any keypress so stray shortcuts don't fire through the overlay.
        if (Vm.ShowCommandPalette && !IsTextEntryElement(Keyboard.FocusedElement))
        {
            Vm.ShowCommandPalette = false;
            e.Handled = true;
            return;
        }

        // V20-32: in peek mode, Escape closes the window outright — there is no chrome to
        // dismiss and no fullscreen to toggle off, so the natural action is exit. Gated BEFORE
        // the regular Escape handler so the normal-mode behavior (clear overlays, return focus)
        // never fires under peek-mode keys.
        if (Vm.IsPeekMode && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (Vm.IsCommandShortcut(CommandIds.CommandPalette, shortcutKey, Keyboard.Modifiers))
        {
            ToggleCommandPaletteFocus();
            e.Handled = true;
            return;
        }

        if (Vm.TryExecuteCommandShortcut(shortcutKey, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        if (Canvas.IsZoomed && Keyboard.Modifiers == ModifierKeys.None && !Vm.IsCropMode)
        {
            const double panStep = 40;
            switch (e.Key)
            {
                case Key.Left: Canvas.PanBy(panStep, 0); e.Handled = true; return;
                case Key.Right: Canvas.PanBy(-panStep, 0); e.Handled = true; return;
                case Key.Up: Canvas.PanBy(0, panStep); e.Handled = true; return;
                case Key.Down: Canvas.PanBy(0, -panStep); e.Handled = true; return;
            }
        }

        switch (e.Key)
        {
            case Key.Escape:
                // A-03: Escape closes any active overlay / toast and returns focus to the
                // window shell. Rename-TextBox Escape is handled inside StemEditor_PreviewKeyDown
                // and never reaches here because the TextBox owns focus.
                if (Vm.IsRetouchMode || Vm.HasRetouchStrokes || Vm.HasRetouchSource)
                {
                    _retouchPainting = false;
                    if (Canvas.IsMouseCaptured)
                        Canvas.ReleaseMouseCapture();
                    Vm.CancelRetouchCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsRedEyeCorrectionMode || Vm.HasRedEyeCorrectionMarks)
                {
                    _redEyeCorrectionPainting = false;
                    if (Canvas.IsMouseCaptured)
                        Canvas.ReleaseMouseCapture();
                    Vm.CancelRedEyeCorrectionCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsExposureBrushMode || Vm.HasExposureBrushStrokes)
                {
                    _exposureBrushPainting = false;
                    if (Canvas.IsMouseCaptured)
                        Canvas.ReleaseMouseCapture();
                    Vm.CancelExposureBrushCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsSelectionMode || Vm.HasCanvasSelection)
                {
                    _canvasSelectionStart = null;
                    if (Canvas.IsMouseCaptured)
                        Canvas.ReleaseMouseCapture();
                    Vm.CancelSelectionCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsCropMode || Vm.HasCropSelection)
                {
                    _cropSelectionStart = null;
                    if (Canvas.IsMouseCaptured)
                        Canvas.ReleaseMouseCapture();
                    Vm.CancelCropCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsSlideshowActive)
                {
                    Vm.StopSlideshow();
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsGalleryOpen)
                {
                    Vm.CloseGalleryCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.IsCompareMode)
                {
                    Vm.ExitCompareCommand.Execute(null);
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.ShowCommandPalette)
                {
                    Vm.ShowCommandPalette = false;
                    Keyboard.ClearFocus();
                    Focus();
                    e.Handled = true;
                    break;
                }
                if (Vm.ShowCheatsheet)
                {
                    Vm.ShowCheatsheet = false;
                }
                if (Vm.IsFullscreen)
                {
                    // Escape also exits fullscreen — standard convention.
                    ToggleFullscreen();
                }
                if (Vm.IsDropTargetActive)
                {
                    Vm.IsDropTargetActive = false;
                    Vm.IsDropAccepted = false;
                }
                if (!string.IsNullOrEmpty(Vm.ToastMessage))
                {
                    Vm.DismissToast();
                }
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
            case Key.OemQuestion:
                // V15-03: ? toggles the cheatsheet. Handling Shift+/ explicitly — OemQuestion
                // is the Shift+/ combo on US layouts; other layouts will need a follow-up.
                Vm.ShowCheatsheet = !Vm.ShowCheatsheet;
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.O when (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt):
                Vm.ExitOverlayModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                // V20-20: Ctrl+F cycles zoom modes Fit → 1:1 → FitWidth → FitHeight → Fill → Fit.
                SetActiveZoomMode(NextZoomMode());
                e.Handled = true;
                break;
            case Key.O when Vm.IsCompareMode && Keyboard.Modifiers == ModifierKeys.None:
                Vm.ToggleCompareOverlayCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.X when Vm.IsCompareMode && Keyboard.Modifiers == ModifierKeys.None:
                Vm.SwapCompareCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemOpenBrackets when Vm.IsCompareMode && Keyboard.Modifiers == ModifierKeys.None:
                Vm.DecreaseCompareOpacityCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemCloseBrackets when Vm.IsCompareMode && Keyboard.Modifiers == ModifierKeys.None:
                Vm.IncreaseCompareOpacityCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && Vm.CopySelectionCommand.CanExecute(null):
                Vm.CopySelectionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsCropMode && Vm.ApplyCropCommand.CanExecute(null):
                Vm.ApplyCropCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsExposureBrushMode:
                Vm.ApplyExposureBrushCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsRedEyeCorrectionMode:
                Vm.ApplyRedEyeCorrectionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsRetouchMode:
                Vm.ApplyRetouchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when Vm.IsGalleryOpen:
                Vm.OpenSelectedGalleryItemCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left when Vm.IsAnimated && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.PreviousAnimationFrameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right when Vm.IsAnimated && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.NextAnimationFrameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when Vm.IsAnimated && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.ToggleAnimationPlaybackCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Back:
                if (Vm.IsArchiveBook) Vm.PrevPageCommand.Execute(null);
                else Vm.PrevCommand.Execute(null);
                e.Handled = true; break;
            case Key.Space:
                if (Vm.IsArchiveBook) Vm.NextPageCommand.Execute(null);
                else Vm.NextCommand.Execute(null);
                e.Handled = true; break;
            case Key.OemPlus:
            case Key.Add:
                ZoomActiveCanvasBy(1.2); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomActiveCanvasBy(1 / 1.2); e.Handled = true; break;
            case Key.D0:
            case Key.NumPad0:
                ResetActiveCanvas(); e.Handled = true; break;
            case Key.D1:
            case Key.NumPad1:
                OneToOneActiveCanvas(); e.Handled = true; break;
        }
    }

    private void AnimationFrame_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement element ||
            element.DataContext is not AnimationFrameItem item)
        {
            return;
        }

        if (Vm.SetAnimationFrameCommand.CanExecute(item.Index))
            Vm.SetAnimationFrameCommand.Execute(item.Index);

        if (!Vm.TryCreateAnimationFrameDragFile(out var path))
            return;

        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { path });
        data.SetText(path);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (Vm.IsRetouchMode)
        {
            HandleRetouchMouseMove(e);
            return;
        }

        if (Vm.IsRedEyeCorrectionMode)
        {
            HandleRedEyeCorrectionMouseMove(e);
            return;
        }

        if (Vm.IsExposureBrushMode)
        {
            HandleExposureBrushMouseMove(e);
            return;
        }

        if (Vm.IsSelectionMode)
        {
            HandleSelectionMouseMove(e);
            return;
        }

        if (Vm.IsCropMode)
        {
            HandleCropMouseMove(e);
            return;
        }

        // RD-07: live pixel readout in the metadata HUD, independent of inspector mode.
        if (Vm.ShowMetadataHud)
        {
            Vm.UpdateHoverPixel(
                TrySampleInspectorPixel(e.GetPosition(Canvas), out var hoverSample) ? hoverSample : null);
        }

        if (!Vm.IsInspectorMode)
            return;

        if (!TrySampleInspectorPixel(e.GetPosition(Canvas), out var sample))
        {
            if (_inspectorSelectionStart is null)
                Vm.UpdateInspectorSample(null);
            return;
        }

        Vm.UpdateInspectorSample(sample);
        if (_inspectorSelectionStart is { } start)
            Vm.UpdateInspectorSelection(PixelInspectorService.CalculateSelection(start, sample.Coordinate));

        e.Handled = true;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Vm.IsRetouchMode)
        {
            HandleRetouchMouseDown(e);
            return;
        }

        if (Vm.IsRedEyeCorrectionMode)
        {
            HandleRedEyeCorrectionMouseDown(e);
            return;
        }

        if (Vm.IsExposureBrushMode)
        {
            HandleExposureBrushMouseDown(e);
            return;
        }

        if (Vm.IsSelectionMode)
        {
            HandleSelectionMouseDown(e);
            return;
        }

        if (Vm.IsCropMode)
        {
            HandleCropMouseDown(e);
            return;
        }

        if (!Vm.IsInspectorMode)
            return;

        if (!TrySampleInspectorPixel(e.GetPosition(Canvas), out var sample))
            return;

        Vm.UpdateInspectorSample(sample);

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _inspectorSelectionStart = sample.Coordinate;
            Vm.UpdateInspectorSelection(PixelInspectorService.CalculateSelection(sample.Coordinate, sample.Coordinate));
            Canvas.CaptureMouse();
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Vm.CopyInspectorSummaryCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_retouchPainting)
        {
            HandleRetouchMouseUp(e);
            return;
        }

        if (_redEyeCorrectionPainting)
        {
            HandleRedEyeCorrectionMouseUp(e);
            return;
        }

        if (_exposureBrushPainting)
        {
            HandleExposureBrushMouseUp(e);
            return;
        }

        if (_canvasSelectionStart is not null)
        {
            HandleSelectionMouseUp(e);
            return;
        }

        if (_cropSelectionStart is not null)
        {
            HandleCropMouseUp(e);
            return;
        }

        if (_inspectorSelectionStart is null)
            return;

        _inspectorSelectionStart = null;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (Vm.IsInspectorMode && _inspectorSelectionStart is null)
            Vm.UpdateInspectorSample(null);
        Vm.UpdateHoverPixel(null);
    }

    private void Canvas_LostMouseCapture(object sender, MouseEventArgs e)
        => ResetCanvasPointerStateAfterCaptureLoss();

    internal void ResetCanvasPointerStateAfterCaptureLoss()
    {
        if (_retouchPainting)
            Vm.CancelActiveRetouchStroke();

        _retouchPainting = false;
        _redEyeCorrectionPainting = false;
        _exposureBrushPainting = false;
        _canvasSelectionStart = null;
        _cropSelectionStart = null;
        _inspectorSelectionStart = null;
    }

    private void HandleRetouchMouseDown(MouseButtonEventArgs e)
    {
        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        var pickSource = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt || !Vm.HasRetouchSource;
        Vm.BeginRetouchStroke(coordinate, pickSource);
        _retouchPainting = !pickSource;
        if (_retouchPainting)
            Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void HandleRetouchMouseMove(MouseEventArgs e)
    {
        if (!_retouchPainting || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Vm.UpdateRetouchStroke(coordinate);
        e.Handled = true;
    }

    private void HandleRetouchMouseUp(MouseButtonEventArgs e)
    {
        if (TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            Vm.EndRetouchStroke(coordinate);

        _retouchPainting = false;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HandleRedEyeCorrectionMouseDown(MouseButtonEventArgs e)
    {
        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        _redEyeCorrectionPainting = true;
        Vm.AddRedEyeCorrectionMark(coordinate);
        Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void HandleRedEyeCorrectionMouseMove(MouseEventArgs e)
    {
        if (!_redEyeCorrectionPainting || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Vm.AddRedEyeCorrectionMark(coordinate);
        e.Handled = true;
    }

    private void HandleRedEyeCorrectionMouseUp(MouseButtonEventArgs e)
    {
        if (TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            Vm.AddRedEyeCorrectionMark(coordinate);

        _redEyeCorrectionPainting = false;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HandleExposureBrushMouseDown(MouseButtonEventArgs e)
    {
        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        _exposureBrushPainting = true;
        Vm.AddExposureBrushStroke(coordinate);
        Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void HandleExposureBrushMouseMove(MouseEventArgs e)
    {
        if (!_exposureBrushPainting || e.LeftButton != MouseButtonState.Pressed)
            return;

        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Vm.AddExposureBrushStroke(coordinate);
        e.Handled = true;
    }

    private void HandleExposureBrushMouseUp(MouseButtonEventArgs e)
    {
        if (TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            Vm.AddExposureBrushStroke(coordinate);

        _exposureBrushPainting = false;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HandleSelectionMouseDown(MouseButtonEventArgs e)
    {
        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Keyboard.ClearFocus();
        Focus();
        _canvasSelectionStart = coordinate;
        Vm.UpdateCanvasSelection(coordinate, coordinate);
        Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void HandleSelectionMouseMove(MouseEventArgs e)
    {
        if (_canvasSelectionStart is not { } start)
            return;

        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Vm.UpdateCanvasSelection(start, coordinate);
        e.Handled = true;
    }

    private void HandleSelectionMouseUp(MouseButtonEventArgs e)
    {
        if (_canvasSelectionStart is { } start &&
            TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
        {
            Vm.UpdateCanvasSelection(start, coordinate);
        }

        _canvasSelectionStart = null;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HandleCropMouseDown(MouseButtonEventArgs e)
    {
        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Keyboard.ClearFocus();
        Focus();
        _cropSelectionStart = coordinate;
        Vm.UpdateCropSelection(coordinate, coordinate);
        Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void HandleCropMouseMove(MouseEventArgs e)
    {
        if (_cropSelectionStart is not { } start)
            return;

        if (!TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
            return;

        Vm.UpdateCropSelection(start, coordinate);
        e.Handled = true;
    }

    private void HandleCropMouseUp(MouseButtonEventArgs e)
    {
        if (_cropSelectionStart is { } start &&
            TryMapCanvasPointToPixel(e.GetPosition(Canvas), out var coordinate))
        {
            Vm.UpdateCropSelection(start, coordinate);
        }

        _cropSelectionStart = null;
        if (Canvas.IsMouseCaptured)
            Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private bool TryMapCanvasPointToPixel(Point viewportPoint, out PixelCoordinate coordinate)
    {
        coordinate = default;

        if (Vm.CurrentImage is not BitmapSource bitmap)
            return false;

        var matrix = Canvas.GetImageToViewportMatrix();
        return PixelInspectorService.TryMapViewportPointToPixel(
            matrix,
            viewportPoint,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            out coordinate);
    }

    private bool TrySampleInspectorPixel(Point viewportPoint, out PixelSample sample)
    {
        sample = default!;

        if (Vm.CurrentImage is not BitmapSource bitmap ||
            !TryMapCanvasPointToPixel(viewportPoint, out var coordinate))
            return false;

        sample = PixelInspectorService.SamplePixel(bitmap, coordinate);
        return true;
    }

    private void StemEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Vm.CommitRenameCommand.Execute(null);
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                Vm.CancelRenameCommand.Execute(null);
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// A-04: Raise UIA TextPatternOnTextSelectionChanged so the Windows Magnifier
    /// tracks the caret position when "Follow the text insertion point" is active.
    /// WPF's TextBoxAutomationPeer raises this internally, but an explicit raise
    /// ensures the event fires on every caret move including mouse clicks and
    /// programmatic selection changes.
    /// </summary>
    private void StemEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            var peer = UIElementAutomationPeer.FromElement(tb)
                       ?? UIElementAutomationPeer.CreatePeerForElement(tb);
            peer?.RaiseAutomationEvent(AutomationEvents.TextPatternOnTextSelectionChanged);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        var accepted = GetDroppedPath(e) is not null;
        Vm.IsDropTargetActive = true;
        Vm.IsDropAccepted = accepted;
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        var accepted = GetDroppedPath(e) is not null;
        Vm.IsDropTargetActive = true;
        Vm.IsDropAccepted = accepted;
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        Vm.IsDropTargetActive = false;
        ClearDragPathVerdict();
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        Vm.IsDropTargetActive = false;
        ClearDragPathVerdict();
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var request = ResolveDropOpenRequest(paths);
        if (!request.IsAccepted) return;

        if (request.Kind == DropOpenKind.Folder)
        {
            e.Handled = true;
            Vm.BrowseFolderCommand.Execute(request.FirstPath);
            return;
        }

        if (request.Kind == DropOpenKind.FileList)
            Vm.OpenFileList(request.Paths);
        else
            Vm.OpenFile(request.FirstPath!);

        e.Handled = true;
    }

    private string? GetDroppedPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0) return null;
        var key = string.Join('\u001f', paths);
        if (string.Equals(_dragPathVerdictKey, key, StringComparison.Ordinal))
            return _dragPathVerdict;

        var verdict = ResolveDropOpenRequest(paths).FirstPath;

        _dragPathVerdictKey = key;
        _dragPathVerdict = verdict;
        return verdict;
    }

    private void ClearDragPathVerdict()
    {
        _dragPathVerdictKey = null;
        _dragPathVerdict = null;
    }

    private static string? GetDroppedFilePath(string path)
    {
        if (!File.Exists(path)) return null;
        var ext = Path.GetExtension(path);
        return Services.DirectoryNavigator.SupportedExtensions.Contains(ext) ? path : null;
    }

    internal static DropOpenRequest ResolveDropOpenRequest(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0)
            return DropOpenRequest.None;

        var first = paths[0];
        if (Directory.Exists(first))
            return new DropOpenRequest(DropOpenKind.Folder, [first]);

        var supportedFiles = paths
            .Select(GetDroppedFilePath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return supportedFiles.Length switch
        {
            0 => DropOpenRequest.None,
            1 => new DropOpenRequest(DropOpenKind.SingleFile, supportedFiles),
            _ => new DropOpenRequest(DropOpenKind.FileList, supportedFiles)
        };
    }

    // ---- V20-29: Command palette event handlers ----

    private void CommandPalette_DimmerClick(object sender, MouseButtonEventArgs e)
    {
        Vm.ShowCommandPalette = false;
    }

    private void CommandPaletteInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                if (Vm.SelectedCommandPaletteIndex > 0)
                    Vm.SelectedCommandPaletteIndex--;
                ScrollCommandPaletteIntoView();
                e.Handled = true;
                break;
            case Key.Down:
                if (Vm.SelectedCommandPaletteIndex < Vm.FilteredCommandPaletteItems.Count - 1)
                    Vm.SelectedCommandPaletteIndex++;
                ScrollCommandPaletteIntoView();
                e.Handled = true;
                break;
            case Key.Enter:
                Vm.ExecuteSelectedPaletteCommand();
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                Vm.ShowCommandPalette = false;
                Keyboard.ClearFocus();
                Focus();
                e.Handled = true;
                break;
        }
    }

    private void CommandPaletteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Vm.ExecuteSelectedPaletteCommand();
        Keyboard.ClearFocus();
        Focus();
    }

    private void ScrollCommandPaletteIntoView()
    {
        if (Vm.SelectedCommandPaletteIndex >= 0 && Vm.SelectedCommandPaletteIndex < Vm.FilteredCommandPaletteItems.Count)
        {
            CommandPaletteList.ScrollIntoView(Vm.FilteredCommandPaletteItems[Vm.SelectedCommandPaletteIndex]);
        }
    }

    private void ChannelChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (Vm.CycleChannelModeCommand.CanExecute(null))
            Vm.CycleChannelModeCommand.Execute(null);
        e.Handled = true;
    }

    // V30-33: slideshow chip interactions — hover pauses the timer, click toggles pause.
    private void SlideshowChip_MouseEnter(object sender, MouseEventArgs e)
    {
        Vm.SlideshowHoverPause();
    }

    private void SlideshowChip_MouseLeave(object sender, MouseEventArgs e)
    {
        Vm.SlideshowHoverResume();
    }

    private void SlideshowChip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Vm.PauseResumeSlideshow();
        e.Handled = true;
    }

    private void MotionVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        Vm.StopMotionVideoCommand.Execute(null);
    }
}
