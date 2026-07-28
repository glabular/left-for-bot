using System.Text;

namespace left_4_bot.Persistence;

/// <summary>
/// Plain-text market announcement bodies (⚠️ in-development).
/// </summary>
public static class MarketAnnouncementFormatter
{
    public static string FormatOpen(int deadlineHourMsk, int starterTabs)
    {
        return
            $"""
            ⚠️ Тотализатор в разработке

            Сегодняшний рынок открыт.
            Категория: {BettingMarket.CategoryTitle}
            Ставки до {deadlineHourMsk}:00 МСК.
            Старт баланса новичкам: {starterTabs} таблов.

            Ставьте на топ-1 в этой категории (только приоритетные игроки).
            """;
    }

    public static string FormatReminder(int deadlineHourMsk, int bank)
    {
        return
            $"""
            ⚠️ Тотализатор в разработке

            Рынок ещё открыт до {deadlineHourMsk}:00 МСК.
            Категория: {BettingMarket.CategoryTitle}
            Банк сейчас: {bank} таблов.
            """;
    }

    public static string FormatClose(int bank)
    {
        return
            $"""
            ⚠️ Тотализатор в разработке

            Ставки закрыты.
            Категория: {BettingMarket.CategoryTitle}
            Банк: {bank} таблов.
            Итог — по статистике gaming day.
            """;
    }

    public static string FormatSettled(
        IReadOnlyList<string> topPlayerLabels,
        IReadOnlyList<string> winnerLines,
        int bank)
    {
        var top = topPlayerLabels.Count == 0
            ? "—"
            : string.Join(", ", topPlayerLabels);

        var winners = new StringBuilder();
        foreach (var line in winnerLines)
        {
            winners.AppendLine($"• {line}");
        }

        return
            $"""
            ⚠️ Тотализатор в разработке

            Итог рынка: {BettingMarket.CategoryTitle}
            Победитель статистики: {top}

            Выиграли ставки:
            {winners.ToString().TrimEnd()}

            Банк {bank} таблов распределён.
            """;
    }

    public static string FormatCancelled(BetSettlementCancelReason reason, int bank)
    {
        var reasonText = reason switch
        {
            BetSettlementCancelReason.TooFewBettors =>
                $"Меньше {ToteEconomy.MinBettorsPerMarket} разных ставящих.",
            BetSettlementCancelReason.NoProtectWinner =>
                "Нет победителя по защите союзников.",
            BetSettlementCancelReason.NoWinningBets =>
                "Никто не угадал топ-1.",
            _ => "Рынок не состоялся.",
        };

        return
            $"""
            ⚠️ Тотализатор в разработке

            Рынок отменён. Ставки возвращены.
            Категория: {BettingMarket.CategoryTitle}
            Причина: {reasonText}
            Банк (возврат): {bank} таблов.
            """;
    }
}
