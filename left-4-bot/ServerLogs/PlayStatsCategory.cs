namespace left_4_bot.ServerLogs;

public sealed class PlayStatsCategory
{
    public PlayStatsCategory(
        string title,
        Func<PlayerDayStats, int> getValue,
        string? warning = null)
    {
        Title = title;
        GetValue = getValue;
        Warning = warning;
    }

    public string Title { get; }

    public Func<PlayerDayStats, int> GetValue { get; }

    public string? Warning { get; }
}
