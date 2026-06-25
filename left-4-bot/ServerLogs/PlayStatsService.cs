using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class PlayStatsService
{
    private readonly ServerLogSyncService _logSync;
    private readonly FriendlyFireStatsService _friendlyFireStats;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly HashSet<string> _prioritySteamIds;

    public PlayStatsService(
        ServerLogSyncService logSync,
        FriendlyFireStatsService friendlyFireStats,
        IOptions<GamingDayOptions> gamingDayOptions,
        IOptions<PriorityPlayersOptions> priorityOptions)
    {
        _logSync = logSync;
        _friendlyFireStats = friendlyFireStats;
        _gamingDayOptions = gamingDayOptions.Value;
        _prioritySteamIds = new HashSet<string>(
            priorityOptions.Value.SteamIds,
            StringComparer.Ordinal);
    }

    public Task<PlayStatsResult> GetTodayStatsAsync(CancellationToken cancellationToken) =>
        GetStatsForGamingDayAsync(GamingDay.GetCurrentAnchor(_gamingDayOptions), cancellationToken);

    public async Task<PlayStatsResult> GetStatsForGamingDayAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayAnchor, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);

        var friendlyFire = await _friendlyFireStats.GetSectionDataAsync(
            gamingDayAnchor,
            _prioritySteamIds,
            cancellationToken);
        var playStatsDownload = await _logSync.DownloadPlayStatsFilesAsync(calendarDates, cancellationToken);

        if (!playStatsDownload.IsSuccess
            && friendlyFire.State == FriendlyFireSectionState.DownloadFailed)
        {
            return PlayStatsReportBuilder.BuildError(
                FormatError(MapDownloadError(playStatsDownload.Message)));
        }

        Dictionary<string, PlayerDayStats>? players = null;
        var playStatsAvailable = playStatsDownload.IsSuccess && playStatsDownload.LocalFilePaths.Count > 0;

        if (playStatsAvailable)
        {
            players = PlayStatsDayAggregator.ParseFiles(playStatsDownload.LocalFilePaths);
        }

        var priorityPlayerPresent = friendlyFire.PriorityPlayerPresent
            || (players?.Keys.Any(_prioritySteamIds.Contains) ?? false);

        if (!priorityPlayerPresent)
        {
            return PlayStatsReportBuilder.BuildNoPriorityPlay(gamingDayAnchor);
        }

        var messages = new List<PlayStatsOutboundMessage>
        {
            new()
            {
                Text = PlayStatsReportFormatter.FormatDayHeader(gamingDayAnchor),
            },
        };

        messages.AddRange(FriendlyFireReportBuilder.BuildMessages(friendlyFire, _prioritySteamIds));

        var includeFriendlyFireInEmpty = friendlyFire.State
            is FriendlyFireSectionState.NoLogs
            or FriendlyFireSectionState.DownloadFailed;

        messages.AddRange(PlayStatsReportBuilder.BuildCategoryMessages(
            players ?? new Dictionary<string, PlayerDayStats>(StringComparer.Ordinal),
            _prioritySteamIds,
            includeFriendlyFireInEmpty));

        return PlayStatsResult.Succeeded(messages);
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
