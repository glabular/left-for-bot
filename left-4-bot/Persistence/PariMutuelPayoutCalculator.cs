namespace left_4_bot.Persistence;

/// <summary>
/// Splits an integer bank among winning stakes with largest-remainder rounding.
/// </summary>
public static class PariMutuelPayoutCalculator
{
    /// <summary>
    /// Credits the full <paramref name="bank"/> to winners proportional to stake.
    /// Remainder tabs go to the largest fractional parts, then lowest TelegramUserId.
    /// </summary>
    public static IReadOnlyList<PariMutuelPayout> Calculate(
        int bank,
        IReadOnlyList<(long TelegramUserId, int Amount)> winningBets)
    {
        if (bank < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bank));
        }

        if (winningBets.Count == 0)
        {
            return [];
        }

        if (bank == 0)
        {
            return winningBets
                .Select(b => new PariMutuelPayout(b.TelegramUserId, b.Amount, 0))
                .ToList();
        }

        long winningStakeTotal = 0;
        foreach (var bet in winningBets)
        {
            if (bet.Amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(winningBets), "Winning stake must be positive.");
            }

            winningStakeTotal += bet.Amount;
        }

        var rows = new List<(long TelegramUserId, int Stake, int Floor, long RemainderScore)>(winningBets.Count);
        var floorSum = 0;

        foreach (var bet in winningBets)
        {
            var exactNumerator = (long)bank * bet.Amount;
            var floor = (int)(exactNumerator / winningStakeTotal);
            var remainderScore = exactNumerator % winningStakeTotal;
            rows.Add((bet.TelegramUserId, bet.Amount, floor, remainderScore));
            floorSum += floor;
        }

        var leftover = bank - floorSum;

        if (leftover < 0 || leftover > winningBets.Count)
        {
            throw new InvalidOperationException("Pari-mutuel floor sum is inconsistent with the bank.");
        }

        var payoutByUser = rows.ToDictionary(r => r.TelegramUserId, r => r.Floor);

        foreach (var row in rows
                     .OrderByDescending(r => r.RemainderScore)
                     .ThenBy(r => r.TelegramUserId)
                     .Take(leftover))
        {
            payoutByUser[row.TelegramUserId] += 1;
        }

        return rows
            .Select(r => new PariMutuelPayout(r.TelegramUserId, r.Stake, payoutByUser[r.TelegramUserId]))
            .ToList();
    }
}
