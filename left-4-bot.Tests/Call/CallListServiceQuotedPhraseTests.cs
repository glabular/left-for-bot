using left_4_bot.Call;

namespace left_4_bot.Tests.Call;

public sealed class CallListServiceQuotedPhraseTests
{
    [Theory]
    [InlineData("\"Го играть!\"", "Го играть!")]
    [InlineData("«Подгребайте»", "Подгребайте")]
    [InlineData("'ping'", "ping")]
    [InlineData("‘ping’", "ping")]
    [InlineData("“ping”", "ping")]
    [InlineData("„ping“", "ping")]
    [InlineData("«Подгребайте\"", "Подгребайте")]
    public void TryParseQuotedPhrase_ValidQuotes_ReturnsInnerPhrase(string args, string expected)
    {
        var ok = CallListService.TryParseQuotedPhrase(args, out var phrase, out var looksLikeQuoted);

        Assert.True(ok);
        Assert.True(looksLikeQuoted);
        Assert.Equal(expected, phrase);
    }

    [Theory]
    [InlineData("«Подгребайте")]
    [InlineData("\"\"")]
    [InlineData("«»")]
    [InlineData("\" \"")]
    public void TryParseQuotedPhrase_InvalidQuoted_SetsLooksLikeQuoted(string args)
    {
        var ok = CallListService.TryParseQuotedPhrase(args, out var phrase, out var looksLikeQuoted);

        Assert.False(ok);
        Assert.True(looksLikeQuoted);
        Assert.Equal(string.Empty, phrase);
    }

    [Theory]
    [InlineData("Подгребайте")]
    [InlineData("@nick")]
    [InlineData("valid_user")]
    public void TryParseQuotedPhrase_NotQuoted_DoesNotLookLikeQuoted(string args)
    {
        var ok = CallListService.TryParseQuotedPhrase(args, out var phrase, out var looksLikeQuoted);

        Assert.False(ok);
        Assert.False(looksLikeQuoted);
        Assert.Equal(string.Empty, phrase);
    }

    [Theory]
    [InlineData("@ab", true)]
    [InlineData("hi", true)]
    [InlineData("valid_user", true)]
    [InlineData("Подгребайте", false)]
    [InlineData("го играть", false)]
    public void LooksLikeUsernameAttempt_ClassifiesFirstToken(string args, bool expected)
    {
        Assert.Equal(expected, CallListService.LooksLikeUsernameAttempt(args));
    }
}
