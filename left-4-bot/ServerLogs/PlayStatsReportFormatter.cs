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

    public const string EmptyCategoriesIntro = "Нет данных:";

    public static string FormatDayHeader(DateOnly gamingDayAnchor) =>
        $"Статистика за {gamingDayAnchor:dd.MM.yyyy} (МСК)";

    public static bool CategoryHasData(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var (priorityPlayers, otherPlayers) = SelectCategoryLeaders(
            category,
            players,
            prioritySteamIds);

        return priorityPlayers.Count > 0 || otherPlayers.Count > 0;
    }

    public static string FormatCategory(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var builder = new StringBuilder();

        builder.AppendLine(FormatCategoryTitle(category.Title));
        builder.AppendLine();

        var (priorityPlayers, otherPlayers) = SelectCategoryLeaders(
            category,
            players,
            prioritySteamIds);

        for (var i = 0; i < priorityPlayers.Count; i++)
        {
            var player = priorityPlayers[i];
            var value = category.GetValue(player);
            builder.AppendLine($"{TopPlaceEmojis[i]} {EscapeHtml(player.Name)} — {value}");
        }

        if (otherPlayers.Count > 0)
        {
            if (priorityPlayers.Count > 0)
            {
                builder.AppendLine();
            }

            foreach (var player in otherPlayers)
            {
                var value = category.GetValue(player);
                builder.AppendLine($"{EscapeHtml(player.Name)} — {value}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatEmptyCategories(IReadOnlyList<PlayStatsCategory> categories)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < categories.Count; i++)
        {
            builder.AppendLine(categories[i].Title);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatNoPriorityPlay(DateOnly gamingDayAnchor) =>
        $"{FormatDayHeader(gamingDayAnchor)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, что не играли.";

    public static string FormatNoLogsForDay(DateOnly gamingDayAnchor) =>
        $"{FormatDayHeader(gamingDayAnchor)}\n\nЗа этот день на сервере нет статистики. Возможно, никто не играл.";

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

    private static string FormatCategoryTitle(string title) =>
        $"<b><u>{EscapeHtml(title)}</u></b>";

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);
}
