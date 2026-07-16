using FluentFTP;

namespace left_4_bot.ServerLogs;

/// <summary>
/// An exclusive, connected FTP session. All FTP commands run through here so that a whole
/// logical operation (list + downloads) is serialized against concurrent callers, because
/// <c>AsyncFtpClient</c> is not thread-safe.
/// </summary>
public interface IFtpSession : IAsyncDisposable
{
    Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken);

    Task DownloadFileAsync(
        string remotePath,
        string localPath,
        FtpLocalExists existsMode,
        CancellationToken cancellationToken);
}
