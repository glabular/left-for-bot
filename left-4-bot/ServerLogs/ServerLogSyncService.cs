using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class ServerLogSyncService
{
    private const string PreferredInboxDirectory = @"D:\temp\left4dead_logs";

    private const string SetupHint =
        "Server log sync not configured. Run:\n" +
        "  dotnet user-secrets set \"ServerLogs:Host\" \"YOUR_HOST\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Port\" \"21\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Username\" \"YOUR_LOGIN\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Password\" \"YOUR_PASSWORD\"\n" +
        "  dotnet user-secrets set \"ServerLogs:RemoteLogDirectory\" \"logs\"";

    private readonly ServerLogSyncOptions _options;
    private readonly IServerFtpClient _ftpClient;

    public ServerLogSyncService(
        IOptions<ServerLogSyncOptions> options,
        IServerFtpClient ftpClient)
    {
        _options = options.Value;
        _ftpClient = ftpClient;
    }

    public Task<TodayLogDownloadResult> DownloadTodayLogFilesAsync(
        CancellationToken cancellationToken) =>
        DownloadTodayLogFilesAsync(MoscowTime.Today, cancellationToken);

    public async Task<TodayLogDownloadResult> DownloadTodayLogFilesAsync(
        DateOnly moscowDate,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateOptions();
        if (validationError is not null)
        {
            return TodayLogDownloadResult.Failed(validationError);
        }

        try
        {
            await _ftpClient.ConnectAsync(cancellationToken);

            var remoteFiles = await _ftpClient.ListFilesAsync(
                _options.RemoteLogDirectory,
                cancellationToken);

            var dateToken = moscowDate.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            var logFiles = remoteFiles
                .Where(file => file.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    && file.Name.Contains(dateToken, StringComparison.Ordinal))
                .OrderBy(file => file.Name)
                .ToList();

            if (logFiles.Count == 0)
            {
                return TodayLogDownloadResult.Succeeded(
                    [],
                    $"За {moscowDate:dd.MM.yyyy} на сервере нет лог-файлов.");
            }

            var localInbox = ResolveLocalInboxDirectory();
            Directory.CreateDirectory(localInbox);

            var localPaths = new List<string>(logFiles.Count);

            foreach (var file in logFiles)
            {
                var localPath = Path.Combine(localInbox, file.Name);

                await _ftpClient.DownloadFileAsync(
                    file.FullPath,
                    localPath,
                    cancellationToken);

                localPaths.Add(localPath);
            }

            return TodayLogDownloadResult.Succeeded(
                localPaths,
                $"Downloaded {logFiles.Count} log file(s) for {moscowDate:dd.MM.yyyy}: " +
                string.Join(", ", logFiles.Select(file => file.Name)));
        }
        catch (Exception ex)
        {
            return TodayLogDownloadResult.Failed(
                $"Не удалось скачать логи с сервера: {ex.Message}");
        }
        finally
        {
            await _ftpClient.DisconnectAsync(cancellationToken);
        }
    }

    public async Task<ServerLogSyncResult> DownloadLatestLogAsync(CancellationToken cancellationToken)
    {
        var download = await DownloadTodayLogFilesAsync(cancellationToken);

        if (!download.IsSuccess)
        {
            return ServerLogSyncResult.Failed(download.Message);
        }

        return ServerLogSyncResult.Succeeded(
            ResolveLocalInboxDirectory(),
            string.Join(", ", download.LocalFilePaths.Select(Path.GetFileName)),
            download.Message);
    }

    private string ResolveLocalInboxDirectory()
    {
        if (IsDriveAvailable('D'))
        {
            return PreferredInboxDirectory;
        }

        return Path.GetFullPath(_options.LocalInboxDirectory);
    }

    private static bool IsDriveAvailable(char driveLetter)
    {
        var driveRoot = $"{driveLetter}:\\";
        return DriveInfo.GetDrives()
            .Any(drive => drive.Name.Equals(driveRoot, StringComparison.OrdinalIgnoreCase));
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
