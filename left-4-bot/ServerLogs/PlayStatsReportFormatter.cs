using System.Net;
using System.Text;

namespace left_4_bot.ServerLogs;

public static class PlayStatsReportFormatter
{
    private const int TopCount = 5;

    private static readonly string[] TopPlaceEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣"];

    public static string Format(
        DateOnly gamingDayLabel,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds,
        IReadOnlyList<PlayStatsCategory> categories)
    {
        var builder = new StringBuilder();

        builder.AppendLine(FormatDayHeader(gamingDayLabel));
        builder.AppendLine();

        for (var i = 0; i < categories.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            AppendCategory(builder, categories[i], players, prioritySteamIds);
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatDayHeader(DateOnly gamingDayLabel) =>
        $"Статистика за {gamingDayLabel:dd.MM.yyyy} (МСК)";

    public static string FormatNoPriorityPlay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, что не играли.";

    public static string FormatNoLogsForDay(DateOnly gamingDayLabel) =>
        $"{FormatDayHeader(gamingDayLabel)}\n\nЗа этот день на сервере нет playstats. Возможно, никто не играл.";

    private static void AppendCategory(
        StringBuilder builder,
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        builder.AppendLine($"<b>{EscapeHtml(category.Title)}</b>");

        var topPlayers = players.Values
            .Where(player => prioritySteamIds.Contains(player.SteamId))
            .Where(player => category.GetValue(player) > 0)
            .OrderByDescending(player => category.GetValue(player))
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopCount)
            .ToList();

        if (topPlayers.Count == 0)
        {
            builder.AppendLine("Нет данных за день");
            return;
        }

        for (var i = 0; i < topPlayers.Count; i++)
        {
            var player = topPlayers[i];
            var value = category.GetValue(player);
            builder.AppendLine($"{TopPlaceEmojis[i]} {EscapeHtml(player.Name)} — {value}");
        }
    }

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);
}
