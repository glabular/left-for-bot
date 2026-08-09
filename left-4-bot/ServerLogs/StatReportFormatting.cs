using System.Net;

namespace left_4_bot.ServerLogs;

public static class StatReportFormatting
{
    public const string FriendlyFireCategoryTitle = "🔪 Дружественный огонь";

    private const string ProtectTeammateCategoryBaseTitle = "Защита союзников";

    private const string ProtectTeammateCategoryExtendedSuffix = " (от обычных и особых заражённых)";

    public static readonly string[] TopPlaceEmojis =
    [
        "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣",
        "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟",
    ];

    public static string GetProtectTeammateCategoryTitle()
    {
        var title = ProtectTeammateCategoryBaseTitle;
        if (Random.Shared.Next(3) == 0)
        {
            title += ProtectTeammateCategoryExtendedSuffix;
        }

        return $"🛡️ {title}";
    }

    public static string FormatCategoryTitle(string title) =>
        $"<b><u>{EscapeHtml(title)}</u></b>";

    public static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);

    public static string FormatTopPlace(int placeIndex) =>
        placeIndex >= 0 && placeIndex < TopPlaceEmojis.Length
            ? TopPlaceEmojis[placeIndex]
            : TopPlaceEmojis[^1];

    public static IEnumerable<(int PlaceIndex, T Entry)> RankTopEntries<T>(
        IEnumerable<T> entries,
        Func<T, int> getScore,
        IComparer<T>? tieBreaker = null,
        int maxCount = 10)
    {
        var ordered = tieBreaker is null
            ? entries.OrderByDescending(getScore)
            : entries.OrderByDescending(getScore).ThenBy(e => e, tieBreaker);

        var list = ordered.Take(maxCount).ToList();
        var placeIndex = 0;

        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0 && getScore(list[i]) != getScore(list[i - 1]))
            {
                placeIndex = i;
            }

            yield return (placeIndex, list[i]);
        }
    }

    public static IEnumerable<(int PlaceIndex, T Entry)> RankAllEntries<T>(
        IEnumerable<T> entries,
        Func<T, int> getScore,
        IComparer<T>? tieBreaker = null)
    {
        var ordered = tieBreaker is null
            ? entries.OrderByDescending(getScore)
            : entries.OrderByDescending(getScore).ThenBy(e => e, tieBreaker);

        var list = ordered.ToList();
        var placeIndex = 0;

        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0 && getScore(list[i]) != getScore(list[i - 1]))
            {
                placeIndex = i;
            }

            yield return (placeIndex, list[i]);
        }
    }

    public static int ResolvePlaceCutoff<T>(
        IReadOnlyList<(int PlaceIndex, T Entry)> ranked,
        Func<T, bool> isPriority,
        int maxPlaceWithPriority,
        int maxPlaceWithoutPriority)
    {
        var worstPriorityPlace = -1;
        var worstPriorityPlaceInWindow = -1;

        foreach (var (placeIndex, entry) in ranked)
        {
            if (!isPriority(entry))
            {
                continue;
            }

            worstPriorityPlace = Math.Max(worstPriorityPlace, placeIndex);

            if (placeIndex <= maxPlaceWithPriority)
            {
                worstPriorityPlaceInWindow = Math.Max(worstPriorityPlaceInWindow, placeIndex);
            }
        }

        if (worstPriorityPlace < 0)
        {
            return maxPlaceWithoutPriority;
        }

        if (worstPriorityPlaceInWindow >= 0)
        {
            return worstPriorityPlaceInWindow;
        }

        return Math.Min(worstPriorityPlace, maxPlaceWithPriority);
    }
}
