using left_4_bot.Persistence;

namespace left_4_bot.Tests.Persistence;

public sealed class MarketAnnouncementFormatterTests
{
    [Fact]
    public void FormatOpen_ContainsWarningDeadlineAndCategory()
    {
        var text = MarketAnnouncementFormatter.FormatOpen(18, 1000);

        Assert.Contains("⚠️ Тотализатор в разработке", text);
        Assert.Contains("Сегодняшний рынок открыт.", text);
        Assert.Contains(BettingMarket.CategoryTitle, text);
        Assert.Contains("Ставки до 18:00 МСК.", text);
        Assert.Contains("1000 таблов", text);
    }

    [Fact]
    public void FormatReminder_ContainsBankAndDeadline()
    {
        var text = MarketAnnouncementFormatter.FormatReminder(18, 250);

        Assert.Contains("⚠️ Тотализатор в разработке", text);
        Assert.Contains("до 18:00 МСК", text);
        Assert.Contains("Банк сейчас: 250 таблов.", text);
    }

    [Fact]
    public void FormatClose_ContainsBank()
    {
        var text = MarketAnnouncementFormatter.FormatClose(400);

        Assert.Contains("Ставки закрыты.", text);
        Assert.Contains("Банк: 400 таблов.", text);
        Assert.Contains(BettingMarket.CategoryTitle, text);
    }

    [Fact]
    public void FormatSettled_ListsWinnersAndBank()
    {
        var text = MarketAnnouncementFormatter.FormatSettled(
            ["Alpha", "Beta"],
            ["@alice: +120 таблов", "@bob: +80 таблов"],
            200);

        Assert.Contains("Итог рынка: " + BettingMarket.CategoryTitle, text);
        Assert.Contains("Победитель статистики: Alpha, Beta", text);
        Assert.Contains("• @alice: +120 таблов", text);
        Assert.Contains("• @bob: +80 таблов", text);
        Assert.Contains("Банк 200 таблов распределён.", text);
    }

    [Fact]
    public void FormatCancelled_IncludesReasonAndBank()
    {
        var text = MarketAnnouncementFormatter.FormatCancelled(
            BetSettlementCancelReason.TooFewBettors,
            150);

        Assert.Contains("Рынок отменён. Ставки возвращены.", text);
        Assert.Contains($"Меньше {ToteEconomy.MinBettorsPerMarket} разных ставящих.", text);
        Assert.Contains("Банк (возврат): 150 таблов.", text);
    }
}
