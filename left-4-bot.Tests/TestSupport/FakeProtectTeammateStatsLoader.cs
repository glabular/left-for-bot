using left_4_bot.ServerLogs;

namespace left_4_bot.Tests.TestSupport;

/// <summary>
/// In-memory stub for settlement tests — no FTP.
/// </summary>
public sealed class FakeProtectTeammateStatsLoader : IProtectTeammateDayStatsLoader
{
    private readonly Dictionary<DateOnly, ProtectTeammateSectionData> _byAnchor = new();

    public void SetStats(DateOnly gamingDayAnchor, ProtectTeammateSectionData data) =>
        _byAnchor[gamingDayAnchor] = data;

    public Task<ProtectTeammateSectionData> LoadAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        if (!_byAnchor.TryGetValue(gamingDayAnchor, out var data))
        {
            throw new InvalidOperationException(
                $"No fake protect stats configured for {gamingDayAnchor:yyyy-MM-dd}.");
        }

        return Task.FromResult(data);
    }
}
