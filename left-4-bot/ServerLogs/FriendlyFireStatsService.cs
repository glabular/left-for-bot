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
                FormatNoLogsForDay(gamingDayLabel, _gamingDayOptions));
        }

        var scan = GamingDayLogParser.ScanFiles(
            download.LocalFilePaths,
            bounds,
            _prioritySteamIds);

        if (!scan.PriorityPlayerPresent)
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatNoPriorityPlay(gamingDayLabel, _gamingDayOptions));
        }

        return FriendlyFireStatsResult.Succeeded(
            scan.Attackers.Count == 0
                ? FormatCleanDay(gamingDayLabel, _gamingDayOptions)
                : FormatStats(gamingDayLabel, _gamingDayOptions, scan.Attackers, _prioritySteamIds),
            useHtmlParseMode: true);
    }

    internal static string FormatStats(
        DateOnly gamingDayLabel,
        GamingDayOptions gamingDayOptions,
        Dictionary<string, FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds)
    {
        var topOrdered = OrderForTop(attackers.Values, prioritySteamIds);
        var orderedForDetails = OrderForDetails(attackers.Values, prioritySteamIds);
        var builder = new StringBuilder();
        var usesGamingDay = gamingDayOptions.GamingDayStartHour != 0
            || gamingDayOptions.GamingDayStartMinute != 0;

        builder.AppendLine(FormatDayHeader(gamingDayLabel, gamingDayOptions));
        builder.AppendLine("Кто попал по своим и сколько раз.");
        builder.AppendLine();

        builder.AppendLine(usesGamingDay
            ? "<b>🔪 Топ за игровой день:</b>"
            : "<b>🔪 Топ за день:</b>");
        builder.AppendLine();

        if (topOrdered.Count == 0)
        {
            builder.AppendLine(usesGamingDay
                ? "Никто из приоритетного списка за этот игровой день не стрелял по своим"
                : "Никто из приоритетного списка сегодня не стрелял по своим");
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
        builder.AppendLine("Подробно:");
        builder.AppendLine();

        foreach (var attacker in orderedForDetails)
        {
            var total = GetAttackerTotal(attacker);
            builder.AppendLine($"{EscapeHtml(attacker.Name)} ({total})");

            foreach (var victim in OrderVictims(attacker))
            {
                builder.AppendLine($"  → {EscapeHtml(victim.Name)}: {victim.HitCount}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static List<FriendlyFireAttackerStats> OrderForTop(
        IEnumerable<FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds) =>
        OrderAttackers(attackers.Where(a => prioritySteamIds.Contains(a.SteamId))).ToList();

    private static List<FriendlyFireAttackerStats> OrderForDetails(
        IEnumerable<FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds)
    {
        var priority = OrderAttackers(attackers.Where(a => prioritySteamIds.Contains(a.SteamId)));
        var others = OrderAttackers(attackers.Where(a => !prioritySteamIds.Contains(a.SteamId)));
        return priority.Concat(others).ToList();
    }

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

    private static string FormatDayHeader(DateOnly gamingDayLabel, GamingDayOptions gamingDayOptions)
    {
        if (gamingDayOptions.GamingDayStartHour == 0 && gamingDayOptions.GamingDayStartMinute == 0)
        {
            return $"Дружественный огонь за {gamingDayLabel:dd.MM.yyyy} (МСК)";
        }

        var startTime = new TimeOnly(gamingDayOptions.GamingDayStartHour, gamingDayOptions.GamingDayStartMinute);
        return $"Дружественный огонь за {gamingDayLabel:dd.MM.yyyy} (игровой день, с {startTime:HH:mm} МСК)";
    }

    private static string FormatCleanDay(DateOnly gamingDayLabel, GamingDayOptions gamingDayOptions)
    {
        var usesGamingDay = gamingDayOptions.GamingDayStartHour != 0
            || gamingDayOptions.GamingDayStartMinute != 0;

        return usesGamingDay
            ? $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nЛоги прочитаны. Попаданий по своим за этот игровой день нет 🎉🕊"
            : $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nЛоги прочитаны. Попаданий по своим за этот день нет 🎉🕊";
    }

    private static string FormatNoPriorityPlay(DateOnly gamingDayLabel, GamingDayOptions gamingDayOptions)
    {
        var usesGamingDay = gamingDayOptions.GamingDayStartHour != 0
            || gamingDayOptions.GamingDayStartMinute != 0;

        return usesGamingDay
            ? $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nСвой состав за этот игровой день на сервере не появлялся. Похоже, сегодня не играли."
            : $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, сегодня не играли.";
    }

    private static string FormatNoLogsForDay(DateOnly gamingDayLabel, GamingDayOptions gamingDayOptions)
    {
        var usesGamingDay = gamingDayOptions.GamingDayStartHour != 0
            || gamingDayOptions.GamingDayStartMinute != 0;

        return usesGamingDay
            ? $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nЗа этот игровой день на сервере нет логов. Возможно, никто не играл."
            : $"{FormatDayHeader(gamingDayLabel, gamingDayOptions)}\n\nЗа этот день на сервере нет логов. Возможно, никто не играл.";
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

public sealed class FriendlyFireStatsResult
{
    public bool IsSuccess { get; init; }

    public bool UseHtmlParseMode { get; init; }

    public string Message { get; init; } = "";

    public static FriendlyFireStatsResult Succeeded(string message, bool useHtmlParseMode = false) =>
        new()
        {
            IsSuccess = true,
            Message = message,
            UseHtmlParseMode = useHtmlParseMode,
        };

    public static FriendlyFireStatsResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
        };
}
