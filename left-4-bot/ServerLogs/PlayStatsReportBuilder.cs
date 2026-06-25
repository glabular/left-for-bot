namespace left_4_bot.ServerLogs;

public static class PlayStatsReportBuilder
{
    public static PlayStatsResult BuildSuccess(
        DateOnly gamingDayAnchor,
        IReadOnlyDictionary<string, PlayerDayStats> players,
        IReadOnlySet<string> prioritySteamIds)
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

        var messages = new List<PlayStatsOutboundMessage>
        {
            new()
            {
                Text = PlayStatsReportFormatter.FormatDayHeader(gamingDayAnchor),
            },
        };

        foreach (var category in withData)
        {
            messages.Add(new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.FormatCategory(category, players, prioritySteamIds),
                UseHtml = true,
                DisableNotification = true,
            });
        }

        if (withoutData.Count > 0)
        {
            messages.Add(new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.EmptyCategoriesIntro,
                DisableNotification = true,
            });

            messages.Add(new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.FormatEmptyCategories(withoutData),
                UseHtml = true,
                DisableNotification = true,
            });
        }

        return PlayStatsResult.Succeeded(messages);
    }

    public static PlayStatsResult BuildNoLogs(DateOnly gamingDayAnchor) =>
        PlayStatsResult.Succeeded(
        [
            new PlayStatsOutboundMessage
            {
                Text = PlayStatsReportFormatter.FormatNoLogsForDay(gamingDayAnchor),
            },
        ]);

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
