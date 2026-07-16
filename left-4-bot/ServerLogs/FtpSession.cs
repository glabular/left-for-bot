using FluentFTP;

namespace left_4_bot.ServerLogs;

internal sealed class FtpSession : IFtpSession
{
    private readonly ServerFtpClient _owner;
    private readonly AsyncFtpClient _client;
    private bool _released;

    public FtpSession(ServerFtpClient owner, AsyncFtpClient client)
    {
        _owner = owner;
        _client = client;
    }

    public async Task<IReadOnlyList<RemoteFileInfo>> ListFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken)
    {
        var items = await _client.GetListing(remoteDirectory, cancellationToken);

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
        var status = await _client.DownloadFile(
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

    public ValueTask DisposeAsync()
    {
        if (!_released)
        {
            _released = true;
            _owner.ReleaseGate();
        }

        return ValueTask.CompletedTask;
    }
}
