using FluentFTP;

namespace left_4_bot.ServerLogs;

public interface IServerFtpClient : IAsyncDisposable
{
    /// <summary>
    /// Acquires exclusive access to the shared FTP connection and ensures it is connected.
    /// The returned session MUST be disposed to release access for other callers.
    /// Disposing the session keeps the underlying connection open for reuse; the connection
    /// is only closed when the client itself is disposed (host shutdown).
    /// </summary>
    Task<IFtpSession> BeginSessionAsync(CancellationToken cancellationToken);
}

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
