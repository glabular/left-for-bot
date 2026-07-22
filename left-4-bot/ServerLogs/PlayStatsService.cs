using left_4_bot.Persistence;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class PlayStatsService
{
    private static readonly TimeSpan PlayStatsParseRetryDelay = TimeSpan.FromSeconds(3);

    private readonly ServerLogSyncService _logSync;
    private readonly FriendlyFireStatsService _friendlyFireStats;
    private readonly ProtectTeammateStatsService _protectTeammateStats;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly PlayersService _playersService;

    public PlayStatsService(
        ServerLogSyncService logSync,
        FriendlyFireStatsService friendlyFireStats,
        ProtectTeammateStatsService protectTeammateStats,
        IOptions<GamingDayOptions> gamingDayOptions,
        PlayersService playersService)
    {
        _logSync = logSync;
        _friendlyFireStats = friendlyFireStats;
        _protectTeammateStats = protectTeammateStats;
        _gamingDayOptions = gamingDayOptions.Value;
        _playersService = playersService;
    }

    public Task<PlayStatsResult> GetCurrentGamingDayStatsAsync(CancellationToken cancellationToken) =>
        GetStatsForGamingDayAsync(GamingDay.GetCurrentAnchor(_gamingDayOptions), cancellationToken);

    public async Task<PlayStatsResult> GetStatsForGamingDayAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        var prioritySteamIds = _playersService.GetPrioritySteamIds();
        var bounds = GamingDay.GetBounds(gamingDayAnchor, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);

        // Download server logs once (shared by friendly-fire and protect-teammate sections),
        // then playstats once. Previously each section opened its own FTP session, which caused
        // three logins per /stat and races with the whois watcher over the shared connection.
        var logDownload = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);
        var playStatsDownload = await _logSync.DownloadPlayStatsFilesAsync(calendarDates, cancellationToken);

        var (friendlyFire, protectTeammate) = BuildLogSections(logDownload, bounds, prioritySteamIds);

        if (!playStatsDownload.IsSuccess
            && friendlyFire.State == FriendlyFireSectionState.DownloadFailed
            && protectTeammate.State == ProtectTeammateSectionState.DownloadFailed)
        {
            return PlayStatsReportBuilder.BuildError(
                FormatError(FormatUserFacingDownloadError(playStatsDownload.Message)));
        }

        Dictionary<string, PlayerDayStats>? players = null;
        var playStatsAvailable = playStatsDownload.IsSuccess && playStatsDownload.LocalFilePaths.Count > 0;

        if (playStatsAvailable)
        {
            var parse = PlayStatsDayAggregator.ParseFiles(playStatsDownload.LocalFilePaths);

            if (parse.FailedPaths.Count > 0)
            {
                await Task.Delay(PlayStatsParseRetryDelay, cancellationToken);

                foreach (var failedPath in parse.FailedPaths)
                {
                    if (File.Exists(failedPath))
                    {
                        File.Delete(failedPath);
                    }
                }

                playStatsDownload = await _logSync.DownloadPlayStatsFilesAsync(
                    calendarDates,
                    cancellationToken);

                if (playStatsDownload.IsSuccess && playStatsDownload.LocalFilePaths.Count > 0)
                {
                    parse = PlayStatsDayAggregator.ParseFiles(playStatsDownload.LocalFilePaths);
                }
            }

            players = parse.Players;
        }

        var priorityPlayerPresent = friendlyFire.PriorityPlayerPresent
            || protectTeammate.PriorityPlayerPresent
            || (players?.Keys.Any(prioritySteamIds.Contains) ?? false);

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

        messages.AddRange(FriendlyFireReportBuilder.BuildMessages(friendlyFire, prioritySteamIds));
        messages.AddRange(ProtectTeammateReportBuilder.BuildMessages(protectTeammate, prioritySteamIds));

        var playersForReport = players ?? new Dictionary<string, PlayerDayStats>(StringComparer.Ordinal);
        var clearsMessage = PlayStatsReportBuilder.BuildCategoryMessageIfHasData(
            PlayStatsCategories.Clears,
            playersForReport,
            prioritySteamIds);
        if (clearsMessage is not null)
        {
            messages.Add(clearsMessage);
        }

        var includeFriendlyFireInEmpty = friendlyFire.State
            is FriendlyFireSectionState.NoLogs
            or FriendlyFireSectionState.DownloadFailed;
        var includeProtectTeammateInEmpty = protectTeammate.State
            is ProtectTeammateSectionState.NoLogs
            or ProtectTeammateSectionState.DownloadFailed;
        var includeClearsInEmpty = clearsMessage is null;

        messages.AddRange(PlayStatsReportBuilder.BuildMainCategoryMessages(
            playersForReport,
            prioritySteamIds,
            includeFriendlyFireInEmpty,
            includeProtectTeammateInEmpty,
            includeClearsInEmpty));

        return PlayStatsResult.Succeeded(messages);
    }

    private (FriendlyFireSectionData FriendlyFire, ProtectTeammateSectionData ProtectTeammate)
        BuildLogSections(
            TodayLogDownloadResult logDownload,
            GamingDayBounds bounds,
            IReadOnlySet<string> prioritySteamIds)
    {
        if (!logDownload.IsSuccess)
        {
            return (
                FriendlyFireSectionData.DownloadFailed(),
                ProtectTeammateSectionData.DownloadFailed());
        }

        if (logDownload.LocalFilePaths.Count == 0)
        {
            return (
                FriendlyFireSectionData.NoLogs(),
                ProtectTeammateSectionData.NoLogs());
        }

        // Parse the downloaded logs a single time and derive both sections from the same scan.
        var scan = GamingDayLogParser.ScanFiles(
            logDownload.LocalFilePaths,
            bounds,
            prioritySteamIds);

        return (
            _friendlyFireStats.CreateFriendlyFireSection(scan),
            _protectTeammateStats.CreateProtectTeammateSection(scan));
    }

    private static string FormatError(string details) =>
        $"⚠️ Не удалось получить статистику.\n\n{details}";

    private static string FormatUserFacingDownloadError(string message)
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
