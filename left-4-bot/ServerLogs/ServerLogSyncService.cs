using System.Globalization;
using System.Net.Sockets;
using FluentFTP;
using FluentFTP.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class ServerLogSyncService
{
    private const string PreferredInboxDirectory = @"D:\temp\left4dead_logs";
    private const int MaxDownloadAttempts = 3;

    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(2);

    private const string SetupHint =
        "Server log sync not configured. Run:\n" +
        "  dotnet user-secrets set \"ServerLogs:Host\" \"YOUR_HOST\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Port\" \"21\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Username\" \"YOUR_LOGIN\"\n" +
        "  dotnet user-secrets set \"ServerLogs:Password\" \"YOUR_PASSWORD\"\n" +
        "  dotnet user-secrets set \"ServerLogs:RemoteLogDirectory\" \"logs\"\n" +
        "  dotnet user-secrets set \"ServerLogs:RemotePlayStatsDirectory\" \"left4dead2/addons/sourcemod/logs\"";

    private readonly ServerLogSyncOptions _options;
    private readonly IServerFtpClient _ftpClient;
    private readonly ILogger<ServerLogSyncService> _logger;

    public ServerLogSyncService(
        IOptions<ServerLogSyncOptions> options,
        IServerFtpClient ftpClient,
        ILogger<ServerLogSyncService> logger)
    {
        _options = options.Value;
        _ftpClient = ftpClient;
        _logger = logger;
    }

    public Task<TodayLogDownloadResult> DownloadTodayLogFilesAsync(
        CancellationToken cancellationToken) =>
        DownloadTodayLogFilesAsync(MoscowTime.Today, cancellationToken);

    public Task<TodayLogDownloadResult> DownloadLogFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        CancellationToken cancellationToken) =>
        DownloadLogFilesAsync(calendarDates, periodDescription: null, cancellationToken);

    public Task<TodayLogDownloadResult> DownloadPlayStatsFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        CancellationToken cancellationToken) =>
        DownloadPlayStatsFilesAsync(calendarDates, periodDescription: null, cancellationToken);

    public Task<TodayLogDownloadResult> DownloadTodayLogFilesAsync(
        DateOnly moscowDate,
        CancellationToken cancellationToken) =>
        DownloadLogFilesAsync([moscowDate], moscowDate.ToString("dd.MM.yyyy"), cancellationToken);

    private Task<TodayLogDownloadResult> DownloadLogFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        string? periodDescription,
        CancellationToken cancellationToken)
    {
        var dateTokens = calendarDates
            .Select(date => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return DownloadRemoteFilesAsync(
            calendarDates,
            periodDescription,
            _options.RemoteLogDirectory,
            ResolveLocalInboxDirectory(),
            file => file.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                && dateTokens.Any(token => file.Name.Contains(token, StringComparison.Ordinal)),
            emptyFilesMessage: datesLabel => $"За {datesLabel} на сервере нет лог-файлов.",
            downloadedFilesMessage: (count, datesLabel, names) =>
                $"Downloaded {count} log file(s) for {datesLabel}: {names}",
            cancellationToken);
    }

    private Task<TodayLogDownloadResult> DownloadPlayStatsFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        string? periodDescription,
        CancellationToken cancellationToken)
    {
        var datePrefixes = calendarDates
            .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return DownloadRemoteFilesAsync(
            calendarDates,
            periodDescription,
            ResolveRemotePlayStatsDirectory(),
            ResolveLocalPlayStatsInboxDirectory(),
            file => file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                && datePrefixes.Any(prefix => file.Name.StartsWith(prefix, StringComparison.Ordinal)),
            emptyFilesMessage: datesLabel => $"За {datesLabel} на сервере нет playstats-файлов.",
            downloadedFilesMessage: (count, datesLabel, names) =>
                $"Downloaded {count} playstats file(s) for {datesLabel}: {names}",
            cancellationToken);
    }

    private async Task<TodayLogDownloadResult> DownloadRemoteFilesAsync(
        IReadOnlyList<DateOnly> calendarDates,
        string? periodDescription,
        string remoteDirectory,
        string localInbox,
        Func<RemoteFileInfo, bool> matchesFile,
        Func<string, string> emptyFilesMessage,
        Func<int, string, string, string> downloadedFilesMessage,
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

        var datesLabel = string.Join(", ", calendarDates.Select(date => date.ToString("dd.MM.yyyy")));

        try
        {
            await _ftpClient.ConnectAsync(cancellationToken);

            _logger.LogDebug("Listing remote directory {RemoteDirectory}", remoteDirectory);

            var remoteFiles = await _ftpClient.ListFilesAsync(
                remoteDirectory,
                cancellationToken);

            var matchedFiles = remoteFiles
                .Where(matchesFile)
                .DistinctBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file.Name)
                .ToList();

            EnsureInboxDirectory(localInbox);

            if (matchedFiles.Count == 0)
            {
                var description = periodDescription ?? datesLabel;

                return TodayLogDownloadResult.Succeeded(
                    [],
                    emptyFilesMessage(description));
            }

            var localPaths = new List<string>(matchedFiles.Count);

            foreach (var file in matchedFiles)
            {
                var localPath = Path.Combine(localInbox, file.Name);
                var localSize = File.Exists(localPath) ? new FileInfo(localPath).Length : 0L;

                if (localSize == file.Size && file.Size > 0)
                {
                    _logger.LogDebug(
                        "Skipping download for {FileName}, size {Size}",
                        file.Name,
                        file.Size);
                    localPaths.Add(localPath);
                    continue;
                }

                var existsMode = localSize > 0 && localSize < file.Size
                    ? FtpLocalExists.Resume
                    : FtpLocalExists.Overwrite;

                await DownloadFileWithRetryAsync(
                    file.FullPath,
                    localPath,
                    existsMode,
                    cancellationToken);

                localPaths.Add(localPath);
            }

            var fileNames = string.Join(", ", matchedFiles.Select(file => file.Name));

            return TodayLogDownloadResult.Succeeded(
                localPaths,
                downloadedFilesMessage(matchedFiles.Count, datesLabel, fileNames));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download server files for dates {Dates}", datesLabel);

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

    private async Task DownloadFileWithRetryAsync(
        string remotePath,
        string localPath,
        FtpLocalExists existsMode,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            try
            {
                await _ftpClient.DownloadFileAsync(
                    remotePath,
                    localPath,
                    existsMode,
                    cancellationToken);

                return;
            }
            catch (Exception ex) when (attempt < MaxDownloadAttempts && IsTransientFtpError(ex, cancellationToken))
            {
                _logger.LogWarning(
                    ex,
                    "FTP download attempt {Attempt}/{MaxAttempts} failed for {RemotePath}",
                    attempt,
                    MaxDownloadAttempts,
                    remotePath);

                await Task.Delay(RetryBaseDelay * attempt, cancellationToken);
                await _ftpClient.ConnectAsync(cancellationToken);
            }
        }
    }

    private static bool IsTransientFtpError(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested
            && exception is OperationCanceledException)
        {
            return false;
        }

        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is TimeoutException or IOException or SocketException or FtpException)
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveLocalInboxDirectory()
    {
        if (IsDriveAvailable('D'))
        {
            return PreferredInboxDirectory;
        }

        return Path.GetFullPath(_options.LocalInboxDirectory);
    }

    private string ResolveLocalPlayStatsInboxDirectory() =>
        Path.GetFullPath(_options.LocalPlayStatsInboxDirectory);

    private string ResolveRemotePlayStatsDirectory()
    {
        var configured = _options.RemotePlayStatsDirectory.Trim();

        if (!string.IsNullOrEmpty(configured)
            && !configured.Equals("addons/sourcemod/logs", StringComparison.Ordinal))
        {
            return configured;
        }

        return DerivePlayStatsDirectoryFromLogDirectory(_options.RemoteLogDirectory);
    }

    internal static string DerivePlayStatsDirectoryFromLogDirectory(string remoteLogDirectory)
    {
        var logDir = remoteLogDirectory.Trim().TrimEnd('/', '\\');

        if (logDir.EndsWith("logs", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = logDir.Length == 4
                ? ""
                : logDir[..^4].TrimEnd('/', '\\');

            const string suffix = "addons/sourcemod/logs";
            return string.IsNullOrEmpty(prefix) ? suffix : $"{prefix}/{suffix}";
        }

        return "left4dead2/addons/sourcemod/logs";
    }

    private static void EnsureInboxDirectory(string inboxDirectory)
    {
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
