using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsService
{
    private const int TopCount = 3;

    private static readonly string[] TopPlaceEmojis = ["1️⃣", "2️⃣", "3️⃣"];

    private readonly ServerLogSyncService _logSync;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly HashSet<string> _prioritySteamIds;

    public FriendlyFireStatsService(
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
        GetStatsForGamingDayAsync(GamingDay.GetCurrentLabel(_gamingDayOptions).AddDays(-1), cancellationToken);

    public async Task<FriendlyFireStatsResult> GetStatsForGamingDayAsync(
        DateOnly gamingDayLabel,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayLabel, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var download = await _logSync.DownloadLogFilesAsync(calendarDates, cancellationToken);

        if (!download.IsSuccess)
        {
            return FriendlyFireStatsResult.Failed(
                FormatError(MapDownloadError(download.Message)));
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatNoLogsForDay(gamingDayLabel));
        }

        var scan = GamingDayLogParser.ScanFiles(
            download.LocalFilePaths,
            bounds,
            _prioritySteamIds);

        if (!scan.PriorityPlayerPresent)
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatNoPriorityPlay(gamingDayLabel));
        }

        return FriendlyFireStatsResult.Succeeded(
            scan.Attackers.Count == 0
                ? FormatCleanDay(gamingDayLabel)
                : FormatStats(gamingDayLabel, scan.Attackers, _prioritySteamIds),
            useHtmlParseMode: true);
    }

    internal static string FormatStats(
        DateOnly gamingDayLabel,
        Dictionary<string, FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds)
    {
        var topOrdered = OrderForTop(attackers.Values, prioritySteamIds);
        var priorityAttackers = OrderAttackers(
            attackers.Values.Where(a => prioritySteamIds.Contains(a.SteamId))).ToList();
        var otherAttackers = OrderAttackers(
            attackers.Values.Where(a => !prioritySteamIds.Contains(a.SteamId))).ToList();
        var builder = new StringBuilder();

        builder.AppendLine(FormatDayHeader(gamingDayLabel));
        builder.AppendLine("Кто попал по своим и сколько раз.");
        builder.AppendLine();

        builder.AppendLine("<b>🔪 Топ за день:</b>");
        builder.AppendLine();

        if (topOrdered.Count == 0)
        {
            builder.AppendLine("Никто из приоритетного списка за этот день не стрелял по своим");
        }
        else
        {
            for (var i = 0; i < Math.Min(TopCount, topOrdered.Count); i++)
            {
                var attacker = topOrdered[i];
                var total = GetAttackerTotal(attacker);
                builder.AppendLine(
                    $"{TopPlaceEmojis[i]} {EscapeHtml(attacker.Name)} — {total} {PluralizeHits(total)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("<b>Подробно:</b>");
        builder.AppendLine();

        foreach (var attacker in priorityAttackers)
        {
            AppendAttackerDetails(builder, attacker);
        }

        if (otherAttackers.Count > 0)
        {
            if (priorityAttackers.Count > 0)
            {
                builder.AppendLine("--------");
                builder.AppendLine();
            }

            builder.AppendLine("<b>Остальные:</b>");
            builder.AppendLine();

            foreach (var attacker in otherAttackers)
            {
                AppendAttackerDetails(builder, attacker);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendAttackerDetails(StringBuilder builder, FriendlyFireAttackerStats attacker)
    {
        var total = GetAttackerTotal(attacker);
        builder.AppendLine($"{EscapeHtml(attacker.Name)} ({total})");

        foreach (var victim in OrderVictims(attacker))
        {
            builder.AppendLine($"  → {EscapeHtml(victim.Name)}: {victim.HitCount}");
        }

        builder.AppendLine();
    }

    private static List<FriendlyFireAttackerStats> OrderForTop(
        IEnumerable<FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds) =>
        OrderAttackers(attackers.Where(a => prioritySteamIds.Contains(a.SteamId))).ToList();

    private static int GetAttackerTotal(FriendlyFireAttackerStats attacker) =>
        attacker.Victims.Values.Sum(v => v.HitCount);

    private static string PluralizeHits(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "попадание";
        }

        if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14))
        {
            return "попадания";
        }

        return "попаданий";
    }

    private static IEnumerable<FriendlyFireAttackerStats> OrderAttackers(
        IEnumerable<FriendlyFireAttackerStats> attackers) =>
        attackers
            .OrderByDescending(GetAttackerTotal)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<FriendlyFireVictimStats> OrderVictims(FriendlyFireAttackerStats attacker) =>
        attacker.Victims.Values
            .OrderByDescending(v => v.HitCount)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase);

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);

    private static string FormatDayHeader(DateOnly gamingDayLabel) =>
        $"Дружественный огонь за {gamingDayLabel:dd.MM.yyyy} (МСК)";

    private static string FormatCleanDay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nЛоги прочитаны. Попаданий по своим за этот день нет 🎉🕊";

    private static string FormatNoPriorityPlay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, сегодня не играли.";

    private static string FormatNoLogsForDay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nЗа этот день на сервере нет логов. Возможно, никто не играл.";

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
