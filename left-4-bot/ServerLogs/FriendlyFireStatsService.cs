using System.Text;

namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsService
{
    private readonly ServerLogSyncService _logSync;

    public FriendlyFireStatsService(ServerLogSyncService logSync)
    {
        _logSync = logSync;
    }

    public async Task<FriendlyFireStatsResult> GetTodayStatsAsync(CancellationToken cancellationToken)
    {
        var moscowDate = MoscowTime.Today;
        var download = await _logSync.DownloadTodayLogFilesAsync(moscowDate, cancellationToken);

        if (!download.IsSuccess)
        {
            return FriendlyFireStatsResult.Failed(
                MapDownloadError(download.Message));
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatEmptyDay(moscowDate));
        }

        var attackers = FriendlyFireLogParser.ParseFiles(download.LocalFilePaths, moscowDate);

        return FriendlyFireStatsResult.Succeeded(FormatStats(moscowDate, attackers));
    }

    internal static string FormatStats(
        DateOnly moscowDate,
        Dictionary<string, FriendlyFireAttackerStats> attackers)
    {
        if (attackers.Count == 0)
        {
            return FormatEmptyDay(moscowDate);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Дружественный огонь за {moscowDate:dd.MM.yyyy} (МСК):");
        builder.AppendLine();

        var orderedAttackers = attackers.Values
            .OrderByDescending(a => a.Victims.Values.Sum(v => v.HitCount))
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var attacker in orderedAttackers)
        {
            builder.AppendLine($"{attacker.Name} ({attacker.SteamId})");

            foreach (var victim in attacker.Victims.Values
                         .OrderByDescending(v => v.HitCount)
                         .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"  {victim.Name} — {victim.HitCount}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatEmptyDay(DateOnly moscowDate) =>
        $"За {moscowDate:dd.MM.yyyy} (МСК) попаданий по своим не найдено 🎉🕊.";

    private static string MapDownloadError(string message) =>
        message.Contains("user-secrets", StringComparison.OrdinalIgnoreCase)
        || message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
            ? "Доступ к логам сервера (FTP) не настроен."
            : message;
}

public sealed class FriendlyFireStatsResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = "";

    public static FriendlyFireStatsResult Succeeded(string message) =>
        new()
        {
            IsSuccess = true,
            Message = message,
        };

    public static FriendlyFireStatsResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
        };
}
