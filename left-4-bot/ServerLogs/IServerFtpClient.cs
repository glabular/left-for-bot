using FluentFTP;

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
        FtpLocalExists existsMode,
        CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);
}
