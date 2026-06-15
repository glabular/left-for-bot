namespace left_4_bot.ServerLogs;

public interface IServerFtpClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken);

    Task DownloadFileAsync(
        string remotePath,
        string localPath,
        CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);
}
