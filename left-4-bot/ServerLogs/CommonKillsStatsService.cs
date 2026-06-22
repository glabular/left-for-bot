using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

public sealed class CommonKillsStatsService
{
    private const int TopCount = 3;

    private static readonly string[] TopPlaceEmojis = ["1️⃣", "2️⃣", "3️⃣"];

    private readonly ServerLogSyncService _logSync;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly HashSet<string> _prioritySteamIds;

    public CommonKillsStatsService(
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
        GetStatsForGamingDayAsync(GamingDay.GetCurrentLabel(_gamingDayOptions), cancellationToken);

    public async Task<FriendlyFireStatsResult> GetStatsForGamingDayAsync(
        DateOnly gamingDayLabel,
        CancellationToken cancellationToken)
    {
        var bounds = GamingDay.GetBounds(gamingDayLabel, _gamingDayOptions);
        var calendarDates = GamingDay.GetCalendarDatesToDownload(bounds);
        var download = await _logSync.DownloadPlayStatsFilesAsync(calendarDates, cancellationToken);

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

        var players = PlayStatsCommonKillsParser.ParseFiles(download.LocalFilePaths);

        if (!players.Keys.Any(_prioritySteamIds.Contains))
        {
            return FriendlyFireStatsResult.Succeeded(
                FormatNoPriorityPlay(gamingDayLabel));
        }

        var activePlayers = players.Values
            .Where(player => player.CommonKills > 0)
            .ToDictionary(player => player.SteamId, StringComparer.Ordinal);

        return FriendlyFireStatsResult.Succeeded(
            activePlayers.Count == 0
                ? FormatCleanDay(gamingDayLabel)
                : FormatStats(gamingDayLabel, activePlayers, _prioritySteamIds),
            useHtmlParseMode: true);
    }

    internal static string FormatStats(
        DateOnly gamingDayLabel,
        Dictionary<string, PlayerCommonKills> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var topOrdered = OrderForTop(players.Values, prioritySteamIds);
        var priorityPlayers = OrderPlayers(
            players.Values.Where(player => prioritySteamIds.Contains(player.SteamId))).ToList();
        var otherPlayers = OrderPlayers(
            players.Values.Where(player => !prioritySteamIds.Contains(player.SteamId))).ToList();
        var builder = new StringBuilder();

        builder.AppendLine(FormatDayHeader(gamingDayLabel));
        builder.AppendLine("Сколько обычных заражённых убил каждый игрок.");
        builder.AppendLine();

        builder.AppendLine("<b>Топ за день:</b>");
        builder.AppendLine();

        if (topOrdered.Count == 0)
        {
            builder.AppendLine("Никто из приоритетного списка за этот день не убивал обычных");
        }
        else
        {
            for (var i = 0; i < Math.Min(TopCount, topOrdered.Count); i++)
            {
                var player = topOrdered[i];
                builder.AppendLine(
                    $"{TopPlaceEmojis[i]} {EscapeHtml(player.Name)} — {player.CommonKills} {PluralizeKills(player.CommonKills)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("<b>Подробно:</b>");
        builder.AppendLine();

        foreach (var player in priorityPlayers)
        {
            builder.AppendLine($"{EscapeHtml(player.Name)} — {player.CommonKills}");
        }

        if (otherPlayers.Count > 0)
        {
            if (priorityPlayers.Count > 0)
            {
                builder.AppendLine("--------");
                builder.AppendLine();
            }

            builder.AppendLine("<b>Остальные:</b>");
            builder.AppendLine();

            foreach (var player in otherPlayers)
            {
                builder.AppendLine($"{EscapeHtml(player.Name)} — {player.CommonKills}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static List<PlayerCommonKills> OrderForTop(
        IEnumerable<PlayerCommonKills> players,
        IReadOnlySet<string> prioritySteamIds) =>
        OrderPlayers(players.Where(player => prioritySteamIds.Contains(player.SteamId))).ToList();

    private static IEnumerable<PlayerCommonKills> OrderPlayers(IEnumerable<PlayerCommonKills> players) =>
        players
            .OrderByDescending(player => player.CommonKills)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase);

    private static string PluralizeKills(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "убийство";
        }

        if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14))
        {
            return "убийства";
        }

        return "убийств";
    }

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);

    private static string FormatDayHeader(DateOnly gamingDayLabel) =>
        $"Обычные за {gamingDayLabel:dd.MM.yyyy} (МСК)";

    private static string FormatCleanDay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nPlaystats прочитаны. Убийств обычных за этот день нет.";

    private static string FormatNoPriorityPlay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, что не играли.";

    private static string FormatNoLogsForDay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nЗа этот день на сервере нет playstats. Возможно, никто не играл.";

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
