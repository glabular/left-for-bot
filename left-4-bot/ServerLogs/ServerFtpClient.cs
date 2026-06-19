using FluentFTP;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class ServerFtpClient : IServerFtpClient
{
    private readonly ServerLogSyncOptions _options;
    private AsyncFtpClient? _client;

    public ServerFtpClient(IOptions<ServerLogSyncOptions> options)
    {
        _options = options.Value;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await DisconnectAsync(cancellationToken);

        _client = new AsyncFtpClient(
            _options.Host,
            _options.Username,
            _options.Password,
            _options.Port);

        ConfigureClient(_client);

        await _client.Connect(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken)
    {
        var client = GetConnectedClient();
        var items = await client.GetListing(remoteDirectory, cancellationToken);

        return items
            .Where(item => item.Type == FtpObjectType.File)
            .Select(item => new RemoteFileInfo(
                item.Name,
                item.FullName,
                item.Size,
                item.Modified))
            .ToList();
    }

    public async Task DownloadFileAsync(
        string remotePath,
        string localPath,
        FtpLocalExists existsMode,
        CancellationToken cancellationToken)
    {
        var client = GetConnectedClient();
        var status = await client.DownloadFile(
            localPath,
            remotePath,
            existsMode,
            FtpVerify.None,
            token: cancellationToken);

        if (status != FtpStatus.Success)
        {
            throw new InvalidOperationException($"FTP download failed for '{remotePath}'.");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        if (_client.IsConnected)
        {
            await _client.Disconnect(cancellationToken);
        }

        _client.Dispose();
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
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
    }

    private AsyncFtpClient GetConnectedClient()
    {
        if (_client is null || !_client.IsConnected)
        {
            throw new InvalidOperationException("FTP client is not connected.");
        }

        return _client;
    }
}
