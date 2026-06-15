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

    public Task<TodayLogDownloadResult> DownloadLogFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        CancellationToken cancellationToken) =>
        DownloadLogFilesAsync(calendarDates, periodDescription: null, cancellationToken);

    public Task<TodayLogDownloadResult> DownloadTodayLogFilesAsync(
        DateOnly moscowDate,
        CancellationToken cancellationToken) =>
        DownloadLogFilesAsync([moscowDate], moscowDate.ToString("dd.MM.yyyy"), cancellationToken);

    private async Task<TodayLogDownloadResult> DownloadLogFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        string? periodDescription,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateOptions();
        if (validationError is not null)
        {
            return TodayLogDownloadResult.Failed(validationError);
        }

        if (calendarDates.Count == 0)
        {
            return TodayLogDownloadResult.Succeeded([], "No calendar dates requested.");
        }

        try
        {
            await _ftpClient.ConnectAsync(cancellationToken);

            var remoteFiles = await _ftpClient.ListFilesAsync(
                _options.RemoteLogDirectory,
                cancellationToken);

            var dateTokens = calendarDates
                .Select(date => date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var logFiles = remoteFiles
                .Where(file => file.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    && dateTokens.Any(token => file.Name.Contains(token, StringComparison.Ordinal)))
                .DistinctBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file.Name)
                .ToList();

            var localInbox = ResolveLocalInboxDirectory();
            ResetInboxDirectory(localInbox);

            if (logFiles.Count == 0)
            {
                var description = periodDescription
                    ?? string.Join(", ", calendarDates.Select(date => date.ToString("dd.MM.yyyy")));

                return TodayLogDownloadResult.Succeeded(
                    [],
                    $"За {description} на сервере нет лог-файлов.");
            }

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

            var downloadedDates = string.Join(", ", calendarDates.Select(date => date.ToString("dd.MM.yyyy")));

            return TodayLogDownloadResult.Succeeded(
                localPaths,
                $"Downloaded {logFiles.Count} log file(s) for {downloadedDates}: " +
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

    private static void ResetInboxDirectory(string inboxDirectory)
    {
        if (Directory.Exists(inboxDirectory))
        {
            Directory.Delete(inboxDirectory, recursive: true);
        }

        Directory.CreateDirectory(inboxDirectory);
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
