using System.Globalization;

namespace left_4_bot.Tests.Handlers;

/// <summary>
/// Documents how /ban and /unban distinguish list indices from bare Steam64.
/// </summary>
public sealed class BanArgumentDisambiguationTests
{
    [Theory]
    [InlineData("1", true, 1)]
    [InlineData("12", true, 12)]
    [InlineData("76561198014791253", false, 0)]
    [InlineData("STEAM_1:1:27262762", false, 0)]
    [InlineData("https://steamcommunity.com/profiles/76561198014791253", false, 0)]
    public void IntParse_OnlySmallIntegersAreListIndexes(string args, bool expectIndex, int expectedIndex)
    {
        var token = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isIndex = false;
        var index = 0;
        if (token.Length == 1)
        {
            isIndex = int.TryParse(token[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        Assert.Equal(expectIndex, isIndex);
        if (expectIndex)
        {
            Assert.Equal(expectedIndex, index);
        }
    }
}
