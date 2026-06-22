using System.Net;
using System.Text;

namespace left_4_bot.ServerLogs;

public static class PlayStatsReportFormatter
{
    private const int TopCount = 10;

    private static readonly string[] TopPlaceEmojis =
    [
        "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣",
        "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟",
    ];

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

        var (priorityPlayers, otherPlayers) = SelectCategoryLeaders(
            category,
            players,
            prioritySteamIds);

        if (priorityPlayers.Count == 0 && otherPlayers.Count == 0)
        {
            builder.AppendLine("Нет данных за день");
            return;
        }

        for (var i = 0; i < priorityPlayers.Count; i++)
        {
            var player = priorityPlayers[i];
            var value = category.GetValue(player);
            builder.AppendLine($"{TopPlaceEmojis[i]} {EscapeHtml(player.Name)} — {value}");
        }

        if (otherPlayers.Count == 0)
        {
            return;
        }

        builder.AppendLine();

        foreach (var player in otherPlayers)
        {
            var value = category.GetValue(player);
            builder.AppendLine($"{EscapeHtml(player.Name)} — {value}");
        }
    }

    private static (List<PlayerDayStats> Priority, List<PlayerDayStats> Others) SelectCategoryLeaders(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var withValue = players.Values
            .Where(player => category.GetValue(player) > 0)
            .ToList();

        var priorityPlayers = withValue
            .Where(player => prioritySteamIds.Contains(player.SteamId))
            .OrderByDescending(player => category.GetValue(player))
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopCount)
            .ToList();

        var remainingSlots = TopCount - priorityPlayers.Count;
        if (remainingSlots <= 0)
        {
            return (priorityPlayers, []);
        }

        var otherPlayers = withValue
            .Where(player => !prioritySteamIds.Contains(player.SteamId))
            .OrderByDescending(player => category.GetValue(player))
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(remainingSlots)
            .ToList();

        return (priorityPlayers, otherPlayers);
    }

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);
}
