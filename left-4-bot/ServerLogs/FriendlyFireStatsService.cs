using System.Net;
using System.Text;

namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsService
{
    private const int TopCount = 3;

    private static readonly string[] TopPlaceEmojis = ["1️⃣", "2️⃣", "3️⃣"];

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
                FormatError(MapDownloadError(download.Message)));
        }

        if (download.LocalFilePaths.Count == 0)
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatNoLogsForDay(moscowDate));
        }

        var attackers = FriendlyFireLogParser.ParseFiles(download.LocalFilePaths, moscowDate);

        return FriendlyFireStatsResult.Succeeded(FormatStats(moscowDate, attackers), useHtmlParseMode: true);
    }

    internal static string FormatStats(
        DateOnly moscowDate,
        Dictionary<string, FriendlyFireAttackerStats> attackers)
    {
        if (attackers.Count == 0)
        {
            return FormatEmptyDay(moscowDate);
        }

        var ordered = OrderAttackers(attackers.Values).ToList();
        var builder = new StringBuilder();

        builder.AppendLine($"Дружественный огонь за {moscowDate:dd.MM.yyyy} (МСК)");
        builder.AppendLine("Кто попал по своим и сколько раз.");
        builder.AppendLine();

        builder.AppendLine("<b>🔪 Топ за день:</b>");
        builder.AppendLine();
        for (var i = 0; i < Math.Min(TopCount, ordered.Count); i++)
        {
            var attacker = ordered[i];
            var total = GetAttackerTotal(attacker);
            builder.AppendLine(
                $"{TopPlaceEmojis[i]} {EscapeHtml(attacker.Name)} — {total} {PluralizeHits(total)}");
        }

        builder.AppendLine();
        builder.AppendLine("Подробно:");
        builder.AppendLine();

        foreach (var attacker in ordered)
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

    private static string FormatDayHeader(DateOnly moscowDate) =>
        $"Дружественный огонь за {moscowDate:dd.MM.yyyy} (МСК)\n" +
        "Кто попал по своим и сколько раз.";

    private static string FormatEmptyDay(DateOnly moscowDate) =>
        $"{FormatDayHeader(moscowDate)}\n\n" +
        "Логи прочитаны. Попаданий по своим за этот день нет 🎉🕊";

    private static string FormatNoLogsForDay(DateOnly moscowDate) =>
        $"{FormatDayHeader(moscowDate)}\n\n" +
        "За этот день на сервере нет логов. Возможно, никто не играл.";

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
