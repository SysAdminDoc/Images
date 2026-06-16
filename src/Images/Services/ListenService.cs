using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// V20-31: local TCP listen service for piped workflows. Binds to <see cref="IPAddress.Loopback"/>
/// (127.0.0.1) only and accepts UTF-8 file paths, one per line. Each valid path is forwarded to
/// the viewer for live open/refresh. All received paths are logged through the network egress
/// panel (P-03) for full transparency.
/// </summary>
public sealed class ListenService : IDisposable
{
    private static readonly ILogger _log = Log.Get("Images.ListenService");

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly Action<string> _onPathReceived;
    private bool _disposed;

    /// <summary>The actual port the listener bound to.</summary>
    public int Port { get; private set; }

    /// <summary>Whether the listener is actively accepting connections.</summary>
    public bool IsListening => _listener is not null && !_disposed;

    public ListenService(Action<string> onPathReceived)
    {
        _onPathReceived = onPathReceived ?? throw new ArgumentNullException(nameof(onPathReceived));
    }

    /// <summary>
    /// Start listening on the specified port. Binds to loopback only (127.0.0.1).
    /// </summary>
    public void Start(int port)
    {
        if (_listener is not null) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _log.LogInformation("listen-mode: bound to tcp://127.0.0.1:{Port}", Port);

        Task.Run(() => AcceptLoop(_cts.Token));
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleClient(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "listen-mode: accept error");
            }
        }
    }

    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break; // client disconnected

                    var path = line.Trim();
                    if (string.IsNullOrEmpty(path)) continue;

                    // Validate: must be a rooted local path, not a URL or UNC.
                    if (!Path.IsPathFullyQualified(path))
                    {
                        _log.LogDebug("listen-mode: rejected non-qualified path");
                        continue;
                    }

                    if (path.StartsWith("\\\\", StringComparison.Ordinal))
                    {
                        _log.LogDebug("listen-mode: rejected UNC path");
                        continue;
                    }

                    if (!File.Exists(path))
                    {
                        _log.LogDebug("listen-mode: rejected non-existent path");
                        continue;
                    }

                    // Log to network egress panel (P-03).
                    NetworkEgressService.Record(
                        $"tcp://127.0.0.1:{Port}",
                        "listen-mode: received path",
                        Encoding.UTF8.GetByteCount(line),
                        0);

                    _log.LogInformation("listen-mode: opening {Path}", path);
                    _onPathReceived(path);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "listen-mode: client error (non-fatal)");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();

        try { _listener?.Stop(); }
        catch { /* best effort */ }

        _listener = null;
        _cts?.Dispose();
        _cts = null;

        _log.LogInformation("listen-mode: stopped");
    }
}
