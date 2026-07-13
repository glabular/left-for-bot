using System.Text;

namespace left_4_bot.ServerLogs;

public static class ProtectTeammateReportBuilder
{
    private const int MaxPlaceIndexWithPriority = 9;
    private const int MaxPlaceIndexWithoutPriority = 2;

    public static IReadOnlyList<PlayStatsOutboundMessage> BuildMessages(
        ProtectTeammateSectionData data,
        IReadOnlySet<string> prioritySteamIds)
    {
        if (data.State is not ProtectTeammateSectionState.HasData)
        {
            return [];
        }

        return
        [
            new PlayStatsOutboundMessage
            {
                Text = FormatCategory(data.Players, prioritySteamIds),
                UseHtml = true,
                DisableNotification = true,
            },
        ];
    }

    private static string FormatCategory(
        IReadOnlyDictionary<string, ProtectTeammatePlayerStats> players,
        IReadOnlySet<string> prioritySteamIds)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            StatReportFormatting.FormatCategoryTitle(StatReportFormatting.GetProtectTeammateCategoryTitle()));
        builder.AppendLine();

        var ranked = StatReportFormatting.RankAllEntries(
            players.Values.Where(player => player.Count > 0),
            player => player.Count,
            PlayerNameComparer).ToList();

        var placeCutoff = StatReportFormatting.ResolvePlaceCutoff(
            ranked,
            player => prioritySteamIds.Contains(player.SteamId),
            MaxPlaceIndexWithPriority,
            MaxPlaceIndexWithoutPriority);

        foreach (var (placeIndex, player) in ranked.Where(entry => entry.PlaceIndex <= placeCutoff))
        {
            var line = FormatPlayerLine(
                player.Name,
                player.Count,
                prioritySteamIds.Contains(player.SteamId));
            builder.AppendLine($"{StatReportFormatting.FormatTopPlace(placeIndex)} {line}");
        }

        return builder.ToString().TrimEnd();
    }

    private static readonly IComparer<ProtectTeammatePlayerStats> PlayerNameComparer =
        Comparer<ProtectTeammatePlayerStats>.Create(
            (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private static string FormatPlayerLine(string name, int value, bool isPriority)
    {
        var line = $"{StatReportFormatting.EscapeHtml(name)} — {value}";
        return isPriority ? $"<b>{line}</b>" : line;
    }
}
