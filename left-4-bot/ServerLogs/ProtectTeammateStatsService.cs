using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class ProtectTeammateStatsService
{
    private readonly ServerLogSyncService _logSync;
    private readonly GamingDayOptions _gamingDayOptions;

    public ProtectTeammateStatsService(
        ServerLogSyncService logSync,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _logSync = logSync;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public async Task<ProtectTeammateSectionData> GetSectionDataAsync(
        DateOnly gamingDayAnchor,
        IReadOnlySet<string> prioritySteamIds,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayAnchor, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var download = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);

        if (!download.IsSuccess)
        {
            return ProtectTeammateSectionData.DownloadFailed();
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return ProtectTeammateSectionData.NoLogs();
        }

        var scan = GamingDayLogParser.ScanFiles(
            download.LocalFilePaths,
            bounds,
            prioritySteamIds);

        if (scan.ProtectTeammatePlayers.Count == 0)
        {
            return ProtectTeammateSectionData.CleanDay(scan.PriorityPlayerPresent);
        }

        return ProtectTeammateSectionData.HasData(
            scan.ProtectTeammatePlayers,
            scan.PriorityPlayerPresent);
    }
}
