using left_4_bot.Persistence;
using left_4_bot.Whois;

namespace left_4_bot.Tests.Persistence;

public sealed class SteamProfileLinkParserTests
{
    [Theory]
    [InlineData("https://steamcommunity.com/profiles/76561198014791253", 76561198014791253UL)]
    [InlineData("76561198014791253", 76561198014791253UL)]
    [InlineData("STEAM_1:1:27262762", 76561198014791253UL)]
    [InlineData("STEAM_0:1:27262762", 76561198014791253UL)]
    public void Parse_Steam64AndClassic_ReturnsSteam64(string input, ulong expectedSteam64)
    {
        var result = SteamProfileLinkParser.Parse(input);

        var parsed = Assert.IsType<SteamLinkParsedSteam64>(result);
        Assert.Equal(expectedSteam64, parsed.Steam64);
    }

    [Fact]
    public void Parse_VanityUrl_ReturnsVanity()
    {
        var result = SteamProfileLinkParser.Parse("https://steamcommunity.com/id/some_name");

        var vanity = Assert.IsType<SteamLinkParsedVanity>(result);
        Assert.Equal("some_name", vanity.Vanity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-steam-id")]
    [InlineData("STEAM_1:2:123")]
    [InlineData("12345")]
    public void Parse_Invalid_ReturnsFailed(string input)
    {
        Assert.IsType<SteamLinkParseFailed>(SteamProfileLinkParser.Parse(input));
    }
}
