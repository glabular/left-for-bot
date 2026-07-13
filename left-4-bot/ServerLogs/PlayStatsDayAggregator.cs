using L4D2PlayStats;

namespace left_4_bot.ServerLogs;

public static class PlayStatsDayAggregator
{
    public static Dictionary<string, PlayerDayStats> ParseFiles(IReadOnlyList<string> filePaths)
    {
        var totals = new Dictionary<string, PlayerDayStats>(StringComparer.Ordinal);

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
                        aggregate = new PlayerDayStats(
                            player.SteamId,
                            player.PlayerName ?? player.SteamId);
                        totals[player.SteamId] = aggregate;
                    }

                    aggregate.Common += player.Common;
                    aggregate.SiKilled += player.SiKilled;
                    aggregate.Headshots += player.HeadshotsSmg
                        + player.HeadshotsSniper
                        + player.HeadshotsPistol;
                    aggregate.WitchDamage += player.WitchDamage;
                    aggregate.TankDamage += player.TankDamage;
                    aggregate.RockSkeets += player.RockSkeets;
                    aggregate.Clears += player.Clears;
                    aggregate.MeleesOnTank += player.MeleesOnTank;

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
