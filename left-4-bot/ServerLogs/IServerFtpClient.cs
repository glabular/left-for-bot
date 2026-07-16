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
