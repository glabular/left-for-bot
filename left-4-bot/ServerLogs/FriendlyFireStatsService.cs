using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsService
{
    private readonly ServerLogSyncService _logSync;
    private readonly GamingDayOptions _gamingDayOptions;

    public FriendlyFireStatsService(
        ServerLogSyncService logSync,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _logSync = logSync;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public async Task<FriendlyFireSectionData> GetSectionDataAsync(
        DateOnly gamingDayAnchor,
        IReadOnlySet<string> prioritySteamIds,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayAnchor, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var download = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);

        if (!download.IsSuccess)
        {
            return FriendlyFireSectionData.DownloadFailed();
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return FriendlyFireSectionData.NoLogs();
        }

        var scan = GamingDayLogParser.ScanFiles(
            download.LocalFilePaths,
            bounds,
            prioritySteamIds);

        if (scan.Attackers.Count == 0)
        {
            return FriendlyFireSectionData.CleanDay(scan.PriorityPlayerPresent);
        }

        return FriendlyFireSectionData.HasHits(scan.Attackers, scan.PriorityPlayerPresent);
    }
}
