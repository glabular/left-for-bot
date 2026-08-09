using left_4_bot.Rcon;

namespace left_4_bot.Tests.Rcon;

public sealed class BanIdListParserTests
{
    [Fact]
    public void Parse_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(BanIdListParser.Parse(""));
        Assert.Empty(BanIdListParser.Parse("   "));
        Assert.Empty(BanIdListParser.Parse("UserID list empty."));
    }

    [Fact]
    public void Parse_PermanentAndTimedRows_ExtractsSlotsAndSteamIds()
    {
        const string listid =
            """
            Banid  #      userid
                 0 STEAM_0:1:11111111 : permanent
                 1 STEAM_1:0:22222222 : 45.0 min
            junk line ignored
            """;

        var entries = BanIdListParser.Parse(listid);

        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries[0].BanSlot);
        Assert.Equal("STEAM_1:1:11111111", entries[0].ClassicSteamId);
        Assert.Equal("навсегда", entries[0].DurationLabel);
        Assert.Equal(1, entries[1].BanSlot);
        Assert.Equal("STEAM_1:0:22222222", entries[1].ClassicSteamId);
        Assert.Equal("45.0 min", entries[1].DurationLabel);
    }

    [Fact]
    public void Parse_SteamWithoutDuration_AllowsNullLabel()
    {
        var entries = BanIdListParser.Parse("  3 STEAM_1:1:999 ");

        Assert.Single(entries);
        Assert.Equal(3, entries[0].BanSlot);
        Assert.Equal("STEAM_1:1:999", entries[0].ClassicSteamId);
        Assert.Null(entries[0].DurationLabel);
    }
}
