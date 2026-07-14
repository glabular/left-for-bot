using System.Text;

namespace left_4_bot.ServerLogs;

public static class PlayStatsReportFormatter
{
    private const int MaxPlaceIndexWithPriority = 9;
    private const int MaxPlaceIndexWithoutPriority = 2;

    public const string EmptyCategoriesIntro = "Нет данных:";

    public static string FormatDayHeader(DateOnly gamingDayAnchor) =>
        $"Статистика за {gamingDayAnchor:dd.MM.yyyy} (МСК)";

    public static bool CategoryHasData(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds) =>
        players.Values.Any(player => category.GetValue(player) > 0);

    public static string FormatCategory(
        PlayStatsCategory category,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var builder = new StringBuilder();

        builder.AppendLine(StatReportFormatting.FormatCategoryTitle(category.Title));

        if (!string.IsNullOrEmpty(category.Warning))
        {
            builder.AppendLine(category.Warning);
        }

        builder.AppendLine();

        var ranked = StatReportFormatting.RankAllEntries(
            players.Values.Where(player => category.GetValue(player) > 0),
            category.GetValue,
            PlayerNameComparer).ToList();

        var placeCutoff = StatReportFormatting.ResolvePlaceCutoff(
            ranked,
            player => prioritySteamIds.Contains(player.SteamId),
            MaxPlaceIndexWithPriority,
            MaxPlaceIndexWithoutPriority);

        foreach (var (placeIndex, player) in ranked.Where(entry => entry.PlaceIndex <= placeCutoff))
        {
            var value = category.GetValue(player);
            var line = FormatPlayerLine(
                player.Name,
                value,
                prioritySteamIds.Contains(player.SteamId));
            builder.AppendLine($"{StatReportFormatting.FormatTopPlace(placeIndex)} {line}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatEmptyCategories(
        IReadOnlyList<PlayStatsCategory> categories,
        bool includeFriendlyFire,
        bool includeProtectTeammate,
        bool includeClears)
    {
        var titles = new List<string>();

        if (includeFriendlyFire)
        {
            titles.Add(StatReportFormatting.FriendlyFireCategoryTitle);
        }

        if (includeProtectTeammate)
        {
            titles.Add(StatReportFormatting.GetProtectTeammateCategoryTitle());
        }

        if (includeClears)
        {
            titles.Add(PlayStatsCategories.Clears.Title);
        }

        foreach (var category in categories)
        {
            titles.Add(category.Title);
        }

        return string.Join(
            Environment.NewLine,
            titles.Select(title => $"· {title}"));
    }

    public static string FormatNoPriorityPlay(DateOnly gamingDayAnchor) =>
        $"{FormatDayHeader(gamingDayAnchor)}\n\nСвой состав за этот день на сервере не появлялся. Похоже, что не играли.";

    private static readonly IComparer<PlayerDayStats> PlayerNameComparer =
        Comparer<PlayerDayStats>.Create(
            (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private static string FormatPlayerLine(string name, int value, bool isPriority)
    {
        var line = $"{StatReportFormatting.EscapeHtml(name)} — {value}";
        return isPriority ? $"<b>{line}</b>" : line;
    }
}
