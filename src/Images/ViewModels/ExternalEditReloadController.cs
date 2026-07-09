using System.IO;
using System.Windows.Threading;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.ViewModels;

public sealed class ExternalEditReloadController : IDisposable
{
    private static readonly ILogger _log = Log.Get(nameof(ExternalEditReloadController));

    public const string ReloadedToastMessage = "Reloaded after external edit";
    public const string ReloadFailedToastPrefix = "External edit reload failed";

    public static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromMilliseconds(800);

    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<Task<bool>> _reload;
    private readonly Action<string> _notify;
    private readonly Func<string, string, FileSystemWatcher> _watcherFactory;
    private readonly DispatcherTimer _debounceTimer;
    private FileSystemWatcher? _watcher;
    private string? _pendingReloadPath;
    private bool _isControllerDisposed;

    public ExternalEditReloadController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<Task<bool>> reload,
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
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
            watcher.Changed += OnExternalEdit;
            watcher.Renamed += OnExternalEdit;
            watcher.EnableRaisingEvents = true;

            _watcher = watcher;
            watcher = null;
            WatchedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _log.LogWarning(ex, "FileSystemWatcher setup failed for {Path}", path);

            if (watcher is not null)
            {
                watcher.Changed -= OnExternalEdit;
                watcher.Renamed -= OnExternalEdit;
                watcher.Dispose();
            }

            _watcher = null;
            WatchedPath = null;
        }
    }

    public void Disarm()
    {
        _debounceTimer.Stop();
        _pendingReloadPath = null;

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
        watcher.Renamed -= OnExternalEdit;
        watcher.Dispose();
    }

    public void ScheduleReload()
        => ScheduleReloadCore(changedPath: null, requirePathMatch: false);

    internal void ScheduleReload(string changedPath)
        => ScheduleReloadCore(changedPath, requirePathMatch: true);

    private void ScheduleReloadCore(string? changedPath, bool requirePathMatch)
    {
        if (IsInactive()) return;
        var normalizedChangedPath = NormalizePath(changedPath);

        try
        {
            if (_uiDispatcher.CheckAccess())
            {
                RestartDebounce(normalizedChangedPath, requirePathMatch);
            }
            else
            {
                _ = _uiDispatcher.BeginInvoke(() =>
                {
                    if (!IsInactive())
                        RestartDebounce(normalizedChangedPath, requirePathMatch);
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
        ScheduleReload(e.FullPath);
    }

    private async void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (IsInactive()) return;
        var pendingReloadPath = _pendingReloadPath;
        _pendingReloadPath = null;
        if (pendingReloadPath is not null && !IsCurrentWatchedPath(pendingReloadPath))
            return;

        try
        {
            if (await _reload())
                _notify(ReloadedToastMessage);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "External edit reload failed.");
            _notify($"{ReloadFailedToastPrefix}: {ex.Message}");
        }
    }

    private void RestartDebounce(string? changedPath, bool requirePathMatch)
    {
        if (requirePathMatch)
        {
            if (changedPath is null || !IsCurrentWatchedPath(changedPath))
                return;

            _pendingReloadPath = changedPath;
        }
        else
        {
            _pendingReloadPath = null;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private bool IsInactive() => _isControllerDisposed || _isDisposed();

    private bool IsCurrentWatchedPath(string changedPath)
    {
        var watchedPath = NormalizePath(WatchedPath);
        return watchedPath is not null &&
               string.Equals(watchedPath, changedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public void Dispose()
    {
        if (_isControllerDisposed) return;

        _isControllerDisposed = true;
        Disarm();
        _debounceTimer.Tick -= OnDebounceTimerTick;
    }
}
