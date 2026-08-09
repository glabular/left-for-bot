using left_4_bot.Rcon;
using left_4_bot.Whois;

namespace left_4_bot.Tests.Rcon;

public sealed class BanIdListFindBySteamTests
{
    [Fact]
    public void Parse_ThenFindByCanonicalSteamId_MatchesAcrossSteam0AndSteam1()
    {
        const string listid =
            """
            Banid  #      userid
                 0 STEAM_0:1:27262762 : permanent
            """;

        var entries = BanIdListParser.Parse(listid);
        Assert.Single(entries);

        Assert.True(SteamIdConverter.TryToCanonicalClassic("STEAM_1:1:27262762", out var query));
        var found = entries.FirstOrDefault(e =>
            string.Equals(e.ClassicSteamId, query, StringComparison.Ordinal));

        Assert.NotNull(found);
        Assert.Equal(0, found.BanSlot);
        Assert.Equal("навсегда", found.DurationLabel);
    }

    [Fact]
    public void Parse_FindMissingSteamId_ReturnsNull()
    {
        const string listid = "0 STEAM_1:0:1 : permanent";
        var entries = BanIdListParser.Parse(listid);

        var found = entries.FirstOrDefault(e =>
            string.Equals(e.ClassicSteamId, "STEAM_1:0:999", StringComparison.Ordinal));

        Assert.Null(found);
    }
}
