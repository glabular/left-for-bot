using FluentFTP;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

/// <summary>
/// Manages a single, long-lived FTP connection that is shared by every caller
/// (whois watcher, /stat, console tools). <c>AsyncFtpClient</c> is not thread-safe, so all
/// access is serialized through <see cref="_gate"/>: only one <see cref="IFtpSession"/> can be
/// active at a time. The TCP/FTP connection is reused between sessions to avoid a storm of
/// re-logins (which some servers reject with "421 Login timeout").
/// </summary>
public sealed class ServerFtpClient : IServerFtpClient
{
    private readonly ServerLogSyncOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AsyncFtpClient? _client;
    private bool _disposed;

    public ServerFtpClient(IOptions<ServerLogSyncOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IFtpSession> BeginSessionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var client = await EnsureConnectedAsync(cancellationToken);

            return new FtpSession(this, client);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    private async Task<AsyncFtpClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is { IsConnected: true })
        {
            return _client;
        }

        if (_client is not null)
        {
            // Connection exists but dropped: try to revive the same instance before recreating.
            try
            {
                await _client.Connect(cancellationToken);

                return _client;
            }
            catch
            {
                await SafeDisposeAsync(_client);
                _client = null;
            }
        }

        var client = new AsyncFtpClient(
            _options.Host,
            _options.Username,
            _options.Password,
            _options.Port);

        ConfigureClient(client);

        await client.Connect(cancellationToken);
        _client = client;

        return _client;
    }

    internal void ReleaseGate()
    {
        _gate.Release();
    }

    public async Task ResetConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_client is not null)
            {
                await SafeDisposeAsync(_client);
                _client = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _gate.WaitAsync();
        try
        {
            if (_client is not null)
            {
                await SafeDisposeAsync(_client);
                _client = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static async Task SafeDisposeAsync(AsyncFtpClient client)
    {
        try
        {
            if (client.IsConnected)
            {
                await client.Disconnect();
            }
        }
        catch
        {
            // Ignore errors while tearing down an already-broken connection.
        }
        finally
        {
            client.Dispose();
        }
    }

    private static void ConfigureClient(AsyncFtpClient client)
    {
        client.Config.RetryAttempts = 3;
        client.Config.ConnectTimeout = 60_000;
        client.Config.ReadTimeout = 120_000;
        client.Config.DataConnectionConnectTimeout = 60_000;
        client.Config.DataConnectionReadTimeout = 120_000;
        client.Config.SocketKeepAlive = true;
        client.Config.StaleDataCheck = true;
        client.Config.DataConnectionType = FtpDataConnectionType.PASV;

        // Reconnect automatically only if the session was lost, instead of re-logging in on
        // every command. This is the FluentFTP-recommended default and avoids login storms.
        client.Config.SelfConnectMode = FtpSelfConnectMode.OnConnectionLost;
    }
}
