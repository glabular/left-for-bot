namespace left_4_bot.ServerLogs;

public sealed class PlayStatsCategory
{
    public PlayStatsCategory(string title, Func<PlayerDayStats, int> getValue)
    {
        Title = title;
        GetValue = getValue;
    }

    public string Title { get; }

    public Func<PlayerDayStats, int> GetValue { get; }
}
