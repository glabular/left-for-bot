using L4D2PlayStats;

namespace left_4_bot.ServerLogs;

public static class PlayStatsCommonKillsParser
{
    public static Dictionary<string, PlayerCommonKills> ParseFiles(IReadOnlyList<string> filePaths)
    {
        var totals = new Dictionary<string, PlayerCommonKills>(StringComparer.Ordinal);

        foreach (var path in filePaths)
        {
            if (!Statistics.TryParse(File.ReadAllText(path), out var stats)
                || stats?.GameRound is null)
            {
                continue;
            }

            foreach (var half in stats.Halves)
            {
                foreach (var player in half.Players)
                {
                    if (string.IsNullOrEmpty(player.SteamId))
                    {
                        continue;
                    }

                    if (!totals.TryGetValue(player.SteamId, out var aggregate))
                    {
                        aggregate = new PlayerCommonKills(
                            player.SteamId,
                            player.PlayerName ?? player.SteamId);
                        totals[player.SteamId] = aggregate;
                    }

                    aggregate.CommonKills += player.Common;

                    if (!string.IsNullOrEmpty(player.PlayerName))
                    {
                        aggregate.Name = player.PlayerName;
                    }
                }
            }
        }

        return totals;
    }
}
