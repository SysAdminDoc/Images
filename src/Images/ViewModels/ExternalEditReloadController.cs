using System.IO;
using System.Windows.Threading;

namespace Images.ViewModels;

public sealed class ExternalEditReloadController : IDisposable
{
    public const string ReloadedToastMessage = "Reloaded after external edit";

    public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(800);

    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<bool> _reload;
    private readonly Action<string> _notify;
    private readonly Func<string, string, FileSystemWatcher> _watcherFactory;
    private readonly DispatcherTimer _debounceTimer;
    private FileSystemWatcher? _watcher;
    private bool _isControllerDisposed;

    public ExternalEditReloadController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<bool> reload,
        Action<string> notify,
        TimeSpan? debounceInterval = null,
        Func<string, string, FileSystemWatcher>? watcherFactory = null)
    {
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _reload = reload ?? throw new ArgumentNullException(nameof(reload));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _watcherFactory = watcherFactory ?? ((directory, fileName) => new FileSystemWatcher(directory, fileName));
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background, _uiDispatcher)
        {
            Interval = debounceInterval ?? DefaultDebounceInterval
        };
        _debounceTimer.Tick += OnDebounceTimerTick;
    }

    public bool IsArmed => _watcher is not null;

    public string? WatchedPath { get; private set; }

    public void Arm(string path)
    {
        Disarm();

        if (IsInactive() || string.IsNullOrWhiteSpace(path))
            return;

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) ||
            string.IsNullOrEmpty(fileName) ||
            !Directory.Exists(directory))
        {
            return;
        }

        FileSystemWatcher? watcher = null;
        try
        {
            watcher = _watcherFactory(directory, fileName);
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += OnExternalEdit;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
            watcher = null;
            WatchedPath = path;
        }
        catch
        {
            if (watcher is not null)
            {
                watcher.Changed -= OnExternalEdit;
                watcher.Dispose();
            }

            _watcher = null;
            WatchedPath = null;
        }
    }

    public void Disarm()
    {
        _debounceTimer.Stop();

        var watcher = _watcher;
        if (watcher is null)
        {
            WatchedPath = null;
            return;
        }

        _watcher = null;
        WatchedPath = null;

        try
        {
            watcher.EnableRaisingEvents = false;
        }
        catch
        {
        }

        watcher.Changed -= OnExternalEdit;
        watcher.Dispose();
    }

    public void ScheduleReload()
    {
        if (IsInactive()) return;

        try
        {
            if (_uiDispatcher.CheckAccess())
            {
                RestartDebounce();
            }
            else
            {
                _ = _uiDispatcher.BeginInvoke(() =>
                {
                    if (!IsInactive())
                        RestartDebounce();
                });
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnExternalEdit(object sender, FileSystemEventArgs e)
    {
        ScheduleReload();
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (IsInactive()) return;

        if (_reload())
            _notify(ReloadedToastMessage);
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private bool IsInactive() => _isControllerDisposed || _isDisposed();

    public void Dispose()
    {
        if (_isControllerDisposed) return;

        _isControllerDisposed = true;
        Disarm();
        _debounceTimer.Tick -= OnDebounceTimerTick;
    }
}
