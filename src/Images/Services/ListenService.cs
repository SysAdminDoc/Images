using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed class ListenService : IDisposable
{
    private static readonly ILogger _log = Log.Get("Images.ListenService");

    private const int MaxLineLength = 32_768;
    private const int MaxConnectionsPerSecond = 20;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly Action<string> _onPathReceived;
    private bool _disposed;

    private int _connectionsThisSecond;
    private long _lastRateTick;

    public int Port { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public bool IsListening => _listener is not null && !_disposed;

    public ListenService(Action<string> onPathReceived)
    {
        _onPathReceived = onPathReceived ?? throw new ArgumentNullException(nameof(onPathReceived));
    }

    public void Start(int port)
    {
        if (_listener is not null) return;

        SessionToken = GenerateToken();
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _log.LogInformation(
            "listen-mode: bound to tcp://127.0.0.1:{Port}, session token: {Token}",
            Port, SessionToken.Length > 8 ? SessionToken[..8] + "..." : SessionToken);

        Task.Run(() => AcceptLoop(_cts.Token));
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);

                if (!CheckRateLimit())
                {
                    _log.LogWarning("listen-mode: connection rate limit exceeded, dropping");
                    client.Dispose();
                    continue;
                }

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

    private const int ConnectionIdleTimeoutSeconds = 300;

    private async Task HandleClient(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

                var authenticated = false;
                while (!ct.IsCancellationRequested)
                {
                    idleCts.CancelAfter(TimeSpan.FromSeconds(ConnectionIdleTimeoutSeconds));
                    var line = await ReadBoundedLine(reader, idleCts.Token);
                    if (line is null) break;

                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (!authenticated)
                    {
                        if (string.Equals(trimmed, SessionToken, StringComparison.Ordinal))
                        {
                            authenticated = true;
                            _log.LogDebug("listen-mode: client authenticated");
                            continue;
                        }

                        _log.LogWarning("listen-mode: rejected unauthenticated client");
                        NetworkEgressService.RecordInbound(
                            $"tcp://127.0.0.1:{Port}",
                            "listen-mode: rejected unauthenticated connection",
                            Encoding.UTF8.GetByteCount(line));
                        return;
                    }

                    if (!TryNormalizeIncomingPath(trimmed, out var path))
                        continue;

                    NetworkEgressService.RecordInbound(
                        $"tcp://127.0.0.1:{Port}",
                        "listen-mode: received path",
                        Encoding.UTF8.GetByteCount(line));

                    _log.LogInformation("listen-mode: opening {Path}", path);
                    _onPathReceived(path);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "listen-mode: client error (non-fatal)");
        }
    }

    private static async Task<string?> ReadBoundedLine(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[1];

        while (!ct.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) return sb.Length > 0 ? sb.ToString() : null;

            if (buffer[0] == '\n') return sb.ToString();
            if (buffer[0] == '\r') continue;

            sb.Append(buffer[0]);
            if (sb.Length > MaxLineLength)
            {
                _log.LogWarning("listen-mode: line exceeded {MaxLen} chars, dropping", MaxLineLength);
                return null;
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    internal static bool TryNormalizeIncomingPath(string input, out string path)
    {
        path = input.Trim();

        if (!Path.IsPathFullyQualified(path))
        {
            _log.LogDebug("listen-mode: rejected non-qualified path");
            return false;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            _log.LogDebug("listen-mode: rejected UNC path");
            return false;
        }

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            _log.LogDebug("listen-mode: rejected path that could not be canonicalized");
            return false;
        }

        if (!File.Exists(path))
        {
            _log.LogDebug("listen-mode: rejected non-existent path");
            return false;
        }

        return true;
    }

    private bool CheckRateLimit()
    {
        var now = Environment.TickCount64;
        if (now - _lastRateTick > 1000)
        {
            _lastRateTick = now;
            _connectionsThisSecond = 0;
        }

        _connectionsThisSecond++;
        return _connectionsThisSecond <= MaxConnectionsPerSecond;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();

        try { _listener?.Stop(); }
        catch { }

        _listener = null;
        _cts?.Dispose();
        _cts = null;

        _log.LogInformation("listen-mode: stopped");
    }
}
