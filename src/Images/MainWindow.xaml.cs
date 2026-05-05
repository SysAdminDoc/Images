using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Images.Services;
using Images.ViewModels;

namespace Images;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        // V20-02: window-state persistence. Restore BEFORE Show so WPF positions first-paint
        // correctly; save on Closing.
        RestoreWindowState();
        Closing += SaveWindowState;
        Closed += (_, _) => Vm.Dispose();
        Vm.FolderPreviewItems.CollectionChanged += (_, _) => QueueCenterCurrentPreviewItems();
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ShowFilmstrip) or nameof(MainViewModel.ShowSideFolderPreview))
            {
                QueueCenterCurrentPreviewItems();
            }
        };

        Viewport.MouseEnter += (_, _) => FadeArrows(1.0);
        Viewport.MouseLeave += (_, _) => FadeArrows(0.0);
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
    }

    public void OpenPath(string path) => Vm.OpenFile(path);

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

    // V20-02: restore saved window geometry; clamp to current working area so a window that
    // was last on a now-disconnected second monitor doesn't land offscreen.
    private void RestoreWindowState()
    {
        var settings = SettingsService.Instance;
        var w = settings.GetDouble(Keys.WindowWidth, Width);
        var h = settings.GetDouble(Keys.WindowHeight, Height);
        var l = settings.GetDouble(Keys.WindowLeft, double.NaN);
        var t = settings.GetDouble(Keys.WindowTop, double.NaN);
        var maximized = settings.GetBool(Keys.WindowMaximized, false);

        // Sanity clamp: width/height must be at least MinWidth/MinHeight; position must be
        // at least partially on-screen (≥ 120 px of the window visible on any work area).
        var wa = System.Windows.SystemParameters.WorkArea;
        if (w >= MinWidth && h >= MinHeight && w <= wa.Width * 4 && h <= wa.Height * 4)
        {
            Width = w; Height = h;
        }

        if (!double.IsNaN(l) && !double.IsNaN(t))
        {
            // 120-px visibility check against the primary work area (multi-monitor check is
            // possible but involves P/Invoke into User32; primary is good enough).
            var visibleL = Math.Max(wa.Left, Math.Min(l, wa.Right - 120));
            var visibleT = Math.Max(wa.Top, Math.Min(t, wa.Bottom - 120));
            Left = visibleL; Top = visibleT;
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        }

        if (maximized) WindowState = System.Windows.WindowState.Maximized;
    }

    private void SaveWindowState(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Vm.IsPeekMode) return;

        var settings = SettingsService.Instance;
        // Only record non-maximized geometry; if the user's maximized, the RestoreBounds holds
        // what they'd get back after unmaximize, so that's what we want to persist.
        var bounds = WindowState == System.Windows.WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        settings.SetDouble(Keys.WindowLeft, bounds.Left);
        settings.SetDouble(Keys.WindowTop, bounds.Top);
        settings.SetDouble(Keys.WindowWidth, bounds.Width);
        settings.SetDouble(Keys.WindowHeight, bounds.Height);
        settings.SetBool(Keys.WindowMaximized, WindowState == System.Windows.WindowState.Maximized);
    }

    // V15-07: F11 toggles fullscreen. Borderless maximized, side panel collapses via the
    // IsFullscreen VM flag. Previous window state is remembered so exit restores it exactly.
    private WindowState _preFullscreenState = WindowState.Normal;
    private WindowStyle _preFullscreenStyle = WindowStyle.SingleBorderWindow;

    private void ToggleFullscreen()
    {
        if (Vm.IsFullscreen)
        {
            // Restore
            WindowStyle = _preFullscreenStyle;
            WindowState = _preFullscreenState;
            ResizeMode = ResizeMode.CanResize;
            Vm.IsFullscreen = false;
        }
        else
        {
            _preFullscreenState = WindowState;
            _preFullscreenStyle = WindowStyle;
            // Normal-state first so the Maximized toggle re-fires on a borderless window even
            // if we were already maximized — otherwise WPF no-ops the state-set.
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Vm.IsFullscreen = true;
        }
    }

    // V15-01: 5-button mouse browsers have trained everyone that XButton1 = back / XButton2 =
    // forward. Hooking PreviewMouseDown at the window level catches the event before any
    // element captures it (e.g. drag-pan on ZoomPanImage). We gate on HasImage via the command
    // CanExecute; when no image is loaded nothing fires. LeftButton / RightButton / MiddleButton
    // are untouched so drag-pan + the V15-02 context menu coexist.
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // TextBoxes should not be hijacked — rename-in-progress shouldn't eat XButton events.
        if (Keyboard.FocusedElement is TextBox) return;

        switch (e.ChangedButton)
        {
            case MouseButton.XButton1:
                Vm.PrevCommand.Execute(null);
                e.Handled = true;
                break;
            case MouseButton.XButton2:
                Vm.NextCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't steal keys from the rename editor.
        if (Keyboard.FocusedElement is TextBox) return;

        // V15-03: any key while the cheatsheet is open dismisses it and swallows the key so
        // the user doesn't trigger whatever shortcut they pressed by accident.
        if (Vm.ShowCheatsheet && e.Key != Key.OemQuestion)
        {
            Vm.ShowCheatsheet = false;
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

        switch (e.Key)
        {
            case Key.Escape:
                // A-03: Escape closes any active overlay / toast and returns focus to the
                // window shell. Rename-TextBox Escape is handled inside StemEditor_PreviewKeyDown
                // and never reaches here because the TextBox owns focus.
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
            case Key.O when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.OpenCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.V when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.PasteFromClipboardCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.OemComma when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Vm.SettingsCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R when (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift):
                // V15-04: Ctrl+Shift+R reload current image.
                Vm.ReloadCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                // V15-10: Ctrl+P print.
                Vm.PrintCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S when (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift):
                // E6: Ctrl+Shift+S save-as-copy.
                Vm.SaveAsCopyCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                // V20-20: Ctrl+F cycles zoom modes Fit → 1:1 → FitWidth → FitHeight → Fill → Fit.
                Canvas.SetZoomMode(NextZoomMode());
                e.Handled = true;
                break;
            case Key.T:
                Vm.ToggleFilmstripCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.I:
                Vm.ToggleMetadataHudCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E:
                // OCR text extraction toggle
                Vm.ExtractTextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left:
                if (Vm.IsArchiveBook) Vm.LeftBookPageTurnCommand.Execute(null);
                else Vm.PrevCommand.Execute(null);
                e.Handled = true; break;
            case Key.Right:
                if (Vm.IsArchiveBook) Vm.RightBookPageTurnCommand.Execute(null);
                else Vm.NextCommand.Execute(null);
                e.Handled = true; break;
            case Key.Back:
                if (Vm.IsArchiveBook) Vm.PrevPageCommand.Execute(null);
                else Vm.PrevCommand.Execute(null);
                e.Handled = true; break;
            case Key.Space:
                if (Vm.IsArchiveBook) Vm.NextPageCommand.Execute(null);
                else Vm.NextCommand.Execute(null);
                e.Handled = true; break;
            case Key.Home:
                if (Vm.IsArchiveBook) Vm.FirstPageCommand.Execute(null);
                else Vm.FirstCommand.Execute(null);
                e.Handled = true; break;
            case Key.End:
                if (Vm.IsArchiveBook) Vm.LastPageCommand.Execute(null);
                else Vm.LastCommand.Execute(null);
                e.Handled = true; break;
            case Key.Delete:
                Vm.DeleteCommand.Execute(null); e.Handled = true; break;
            case Key.F5:
                Vm.RefreshCommand.Execute(null); e.Handled = true; break;
            case Key.OemPlus:
            case Key.Add:
                Canvas.ZoomBy(1.2); e.Handled = true; break;
            case Key.OemMinus:
            case Key.Subtract:
                Canvas.ZoomBy(1 / 1.2); e.Handled = true; break;
            case Key.D0:
            case Key.NumPad0:
                Canvas.ResetView(); e.Handled = true; break;
            case Key.D1:
            case Key.NumPad1:
                Canvas.OneToOne(); e.Handled = true; break;
        }
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
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        Vm.IsDropTargetActive = false;
        var path = GetDroppedPath(e);
        if (path is null) return;
        Vm.OpenFile(path);
        e.Handled = true;
    }

    private static string? GetDroppedPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0) return null;
        var first = paths[0];
        if (!File.Exists(first)) return null;
        var ext = Path.GetExtension(first);
        return Services.DirectoryNavigator.SupportedExtensions.Contains(ext) ? first : null;
    }
}
