namespace left_4_bot.ServerLogs;

public static class PlayStatsReportBuilder
{
    public static IReadOnlyList<PlayStatsOutboundMessage> BuildCategoryMessages(
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds,
        bool includeFriendlyFireInEmpty)
    {
        var withData = new List<PlayStatsCategory>();
        var withoutData = new List<PlayStatsCategory>();

        foreach (var category in PlayStatsCategories.All)
        {
            if (PlayStatsReportFormatter.CategoryHasData(category, players, prioritySteamIds))
            {
                withData.Add(category);
            }
            else
            {
                withoutData.Add(category);
            }
        }

        var messages = new List<PlayStatsOutboundMessage>();

        foreach (var category in withData)
        {
            messages.Add(new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.FormatCategory(category, players, prioritySteamIds),
                UseHtml = true,
                DisableNotification = true,
            });
        }

        if (!includeFriendlyFireInEmpty && withoutData.Count == 0)
        {
            return messages;
        }

        messages.Add(new PlayStatsOutboundMessage
        {
            Text = PlayStatsReportFormatter.EmptyCategoriesIntro,
            DisableNotification = true,
        });

        messages.Add(new PlayStatsOutboundMessage
        {
            Text = PlayStatsReportFormatter.FormatEmptyCategories(withoutData, includeFriendlyFireInEmpty),
            DisableNotification = true,
        });

        return messages;
    }

    public static PlayStatsResult BuildNoPriorityPlay(DateOnly gamingDayAnchor) =>
        PlayStatsResult.Succeeded(
        [
            new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.FormatNoPriorityPlay(gamingDayAnchor),
            },
        ]);

    public static PlayStatsResult BuildError(string message) =>
        PlayStatsResult.Failed(message);
}
