using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class PlayStatsService
{
    private readonly ServerLogSyncService _logSync;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly HashSet<string> _prioritySteamIds;

    public PlayStatsService(
        ServerLogSyncService logSync,
        IOptions<GamingDayOptions> gamingDayOptions,
        IOptions<PriorityPlayersOptions> priorityOptions)
    {
        _logSync = logSync;
        _gamingDayOptions = gamingDayOptions.Value;
        _prioritySteamIds = new HashSet<string>(
            priorityOptions.Value.SteamIds,
            StringComparer.Ordinal);
    }

    public Task<FriendlyFireStatsResult> GetTodayStatsAsync(CancellationToken cancellationToken) =>
        GetStatsForGamingDayAsync(GamingDay.GetCurrentLabel(_gamingDayOptions), cancellationToken);

    public async Task<FriendlyFireStatsResult> GetStatsForGamingDayAsync(
        DateOnly gamingDayLabel,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayLabel, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var download = await _logSync.DownloadPlayStatsFilesAsync(calendarDates, cancellationToken);

        if (!download.IsSuccess)
        {
            return FriendlyFireStatsResult.Failed(
                FormatError(MapDownloadError(download.Message)));
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return FriendlyFireStatsResult.Succeeded(
                PlayStatsReportFormatter.FormatNoLogsForDay(gamingDayLabel));
        }

        var players = PlayStatsDayAggregator.ParseFiles(download.LocalFilePaths);

        if (!players.Keys.Any(_prioritySteamIds.Contains))
        {
            return FriendlyFireStatsResult.Succeeded(
                PlayStatsReportFormatter.FormatNoPriorityPlay(gamingDayLabel));
        }

        return FriendlyFireStatsResult.Succeeded(
            PlayStatsReportFormatter.Format(
                gamingDayLabel,
                players,
                _prioritySteamIds,
                PlayStatsCategories.All),
            useHtmlParseMode: true);
    }

    private static string FormatError(string details) =>
        $"⚠️ Не удалось получить статистику.\n\n{details}";

    private static string MapDownloadError(string message)
    {
        if (message.Contains("user-secrets", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return "Доступ к логам сервера (FTP) не настроен.";
        }

        if (message.Contains("Не удалось скачать", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message}\nПопробуйте позже или сообщите администратору.";
        }

        return message;
    }
}
