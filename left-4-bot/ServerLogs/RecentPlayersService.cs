using left_4_bot.Persistence;
using left_4_bot.Whois;

namespace left_4_bot.ServerLogs;

/// <summary>
/// Loads unique players who appeared in server logs during the last 24 Moscow hours.
/// </summary>
public sealed class RecentPlayersService
{
    private readonly ServerLogSyncService _logSync;
    private readonly PlayersService _playersService;

    public RecentPlayersService(
        ServerLogSyncService logSync,
        PlayersService playersService)
    {
        _logSync = logSync;
        _playersService = playersService;
    }

    /// <summary>
    /// Downloads logs covering the last 24 Moscow wall-clock hours and scans for players.
    /// Priority players are excluded from the result.
    /// </summary>
    public async Task<RecentPlayersQueryResult> GetPlayersLast24HoursAsync(
        CancellationToken cancellationToken)
    {
        var end = GamingDay.NowMoscow();
        var start = end.AddHours(-24);
        var bounds = new GamingDayBounds(start, end);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);

        var download = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);
        if (!download.IsSuccess)
        {
            return RecentPlayersQueryResult.Failed(download.Message);
        }

        var priority = BuildPriorityCanonicalSet(_playersService.GetPrioritySteamIds());
        var players = RecentPlayersLogScanner
            .ScanFiles(download.LocalFilePaths, bounds)
            .Where(p => !priority.Contains(p.ClassicSteamId))
            .ToList();

        return RecentPlayersQueryResult.Succeeded(players, download.Message);
    }

    private static HashSet<string> BuildPriorityCanonicalSet(IReadOnlySet<string> prioritySteamIds)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in prioritySteamIds)
        {
            if (SteamIdConverter.TryToCanonicalClassic(id, out var canonical))
            {
                set.Add(canonical);
            }
            else
            {
                set.Add(id);
            }
        }

        return set;
    }
}
