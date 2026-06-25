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
        IReadOnlySet<string> prioritySteamIds) =>
        SelectCategoryLeaders(category, players).Count > 0;

    public static string FormatCategory(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var builder = new StringBuilder();

        builder.AppendLine(FormatCategoryTitle(category.Title));
        builder.AppendLine();

        var leaders = SelectCategoryLeaders(category, players);

        for (var i = 0; i < leaders.Count; i++)
        {
            var player = leaders[i];
            var value = category.GetValue(player);
            var line = FormatPlayerLine(
                player.Name,
                value,
                prioritySteamIds.Contains(player.SteamId));
            builder.AppendLine($"{TopPlaceEmojis[i]} {line}");
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

    private static List<PlayerDayStats> SelectCategoryLeaders(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players) =>
        players.Values
            .Where(player => category.GetValue(player) > 0)
            .OrderByDescending(player => category.GetValue(player))
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopCount)
            .ToList();

    private static string FormatCategoryTitle(string title) =>
        $"<b><u>{EscapeHtml(title)}</u></b>";

    private static string FormatPlayerLine(string name, int value, bool isPriority)
    {
        var line = $"{EscapeHtml(name)} — {value}";
        return isPriority ? $"<b>{line}</b>" : line;
    }

    private static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);
}
