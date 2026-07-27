using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

/// <summary>
/// Downloads server logs for a gaming day and aggregates protect-teammate counts.
/// Does not download playstats files.
/// </summary>
public sealed class ProtectTeammateDayStatsLoader : IProtectTeammateDayStatsLoader
{
    private static readonly IReadOnlySet<string> EmptyPriority =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly ServerLogSyncService _logSync;
    private readonly ProtectTeammateStatsService _protectTeammateStats;
    private readonly GamingDayOptions _gamingDayOptions;

    public ProtectTeammateDayStatsLoader(
        ServerLogSyncService logSync,
        ProtectTeammateStatsService protectTeammateStats,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _logSync = logSync;
        _protectTeammateStats = protectTeammateStats;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public async Task<ProtectTeammateSectionData> LoadAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayAnchor, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var logDownload = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);

        if (!logDownload.IsSuccess)
        {
            return ProtectTeammateSectionData.DownloadFailed();
        }

        if (logDownload.LocalFilePaths.Count == 0)
        {
            return ProtectTeammateSectionData.NoLogs();
        }

        var scan = GamingDayLogParser.ScanFiles(
            logDownload.LocalFilePaths,
            bounds,
            EmptyPriority);

        return _protectTeammateStats.CreateProtectTeammateSection(scan);
    }
}
