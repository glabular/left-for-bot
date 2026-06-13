using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class ServerLogSyncService
{
    private const string SetupHint =
        "Server log sync not configured. Run:\n" +
        "  dotnet user-secrets set \"ServerLogs:Host\" \"YOUR_HOST\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Port\" \"21\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Username\" \"YOUR_LOGIN\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Password\" \"YOUR_PASSWORD\"\n" +
        "  dotnet user-secrets set \"ServerLogs:RemoteLogDirectory\" \"left4dead2\"";

    private readonly ServerLogSyncOptions _options;
    private readonly IServerFtpClient _ftpClient;

    public ServerLogSyncService(
        IOptions<ServerLogSyncOptions> options,
        IServerFtpClient ftpClient)
    {
        _options = options.Value;
        _ftpClient = ftpClient;
    }

    public async Task<ServerLogSyncResult> DownloadLatestLogAsync(CancellationToken cancellationToken)
    {
        var validationError = ValidateOptions();
        if (validationError is not null)
        {
            return ServerLogSyncResult.Failed(validationError);
        }

        try
        {
            await _ftpClient.ConnectAsync(cancellationToken);

            var remoteFiles = await _ftpClient.ListFilesAsync(
                _options.RemoteLogDirectory,
                cancellationToken);

            var textFiles = remoteFiles
                .Where(file => file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.Name)
                .ToList();

            if (textFiles.Count == 0)
            {
                return ServerLogSyncResult.Failed(
                    $"No .txt files found in '{_options.RemoteLogDirectory}'.");
            }

            var localInbox = Path.GetFullPath(_options.LocalInboxDirectory);
            Directory.CreateDirectory(localInbox);

            foreach (var file in textFiles)
            {
                var localPath = Path.Combine(localInbox, file.Name);

                await _ftpClient.DownloadFileAsync(
                    file.FullPath,
                    localPath,
                    cancellationToken);
            }

            return ServerLogSyncResult.Succeeded(
                localInbox,
                string.Join(", ", textFiles.Select(file => file.Name)),
                $"Downloaded {textFiles.Count} file(s) to {localInbox}: " +
                string.Join(", ", textFiles.Select(file => file.Name)));
        }
        catch (Exception ex)
        {
            return ServerLogSyncResult.Failed($"Server log sync failed: {ex.Message}");
        }
        finally
        {
            await _ftpClient.DisconnectAsync(cancellationToken);
        }
    }

    private string? ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            return SetupHint;
        }

        return null;
    }
}
