using left_4_bot.Persistence;

namespace left_4_bot.Tests.Persistence;

public sealed class PariMutuelPayoutCalculatorTests
{
    [Fact]
    public void SingleWinner_GetsFullBank()
    {
        var payouts = PariMutuelPayoutCalculator.Calculate(
            bank: 100,
            [(1L, 40)]);

        Assert.Single(payouts);
        Assert.Equal(100, payouts[0].Payout);
        Assert.Equal(40, payouts[0].Stake);
        Assert.Equal(1, payouts[0].TelegramUserId);
    }

    [Fact]
    public void ProportionalSplit_10_30_100()
    {
        const int bank = 140;
        var payouts = PariMutuelPayoutCalculator.Calculate(
            bank,
            [
                (1, 10),
                (2, 30),
                (3, 100),
            ]);

        Assert.Equal(3, payouts.Count);
        Assert.Equal(10, payouts.Single(p => p.TelegramUserId == 1).Payout);
        Assert.Equal(30, payouts.Single(p => p.TelegramUserId == 2).Payout);
        Assert.Equal(100, payouts.Single(p => p.TelegramUserId == 3).Payout);
        Assert.Equal(bank, payouts.Sum(p => p.Payout));
    }

    [Fact]
    public void Remainder_GoesToLargestFraction_ThenLowestUserId()
    {
        // bank=10, stakes 10/30/100 → floors 0/2/7, leftover 1 → +1 to stake 10 (largest remainder).
        const int bank = 10;
        var payouts = PariMutuelPayoutCalculator.Calculate(
            bank,
            [
                (1, 10),
                (2, 30),
                (3, 100),
            ]);

        Assert.Equal(1, payouts.Single(p => p.TelegramUserId == 1).Payout);
        Assert.Equal(2, payouts.Single(p => p.TelegramUserId == 2).Payout);
        Assert.Equal(7, payouts.Single(p => p.TelegramUserId == 3).Payout);
        Assert.Equal(bank, payouts.Sum(p => p.Payout));

        // Equal fractional parts: leftover goes to lowest TelegramUserId.
        var tied = PariMutuelPayoutCalculator.Calculate(
            bank: 10,
            [
                (30, 1),
                (20, 1),
                (10, 1),
            ]);

        Assert.Equal(4, tied.Single(p => p.TelegramUserId == 10).Payout);
        Assert.Equal(3, tied.Single(p => p.TelegramUserId == 20).Payout);
        Assert.Equal(3, tied.Single(p => p.TelegramUserId == 30).Payout);
        Assert.Equal(10, tied.Sum(p => p.Payout));
    }

    [Fact]
    public void TieTargets_MultipleWinners()
    {
        const int bank = 90;
        var payouts = PariMutuelPayoutCalculator.Calculate(
            bank,
            [
                (1, 10),
                (2, 20),
            ]);

        Assert.Equal(30, payouts.Single(p => p.TelegramUserId == 1).Payout);
        Assert.Equal(60, payouts.Single(p => p.TelegramUserId == 2).Payout);
        Assert.Equal(bank, payouts.Sum(p => p.Payout));
    }

    [Fact]
    public void EmptyWinners_ReturnsEmpty()
    {
        var payouts = PariMutuelPayoutCalculator.Calculate(100, []);
        Assert.Empty(payouts);
    }

    [Fact]
    public void ZeroBank_ReturnsZeroPayouts()
    {
        var payouts = PariMutuelPayoutCalculator.Calculate(
            bank: 0,
            [
                (1, 10),
                (2, 20),
            ]);

        Assert.Equal(2, payouts.Count);
        Assert.All(payouts, p => Assert.Equal(0, p.Payout));
        Assert.Equal(10, payouts.Single(p => p.TelegramUserId == 1).Stake);
        Assert.Equal(20, payouts.Single(p => p.TelegramUserId == 2).Stake);
    }
}
