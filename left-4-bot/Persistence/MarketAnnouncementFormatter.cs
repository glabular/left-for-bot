using System.Text;

namespace left_4_bot.Persistence;

/// <summary>
/// Plain-text market announcement bodies (⚠️ in-development).
/// </summary>
public static class MarketAnnouncementFormatter
{
    private const string InDevelopmentFooter = "⚠️ Тотализатор в разработке";

    private static string WithFooter(string body) =>
        $"{body.TrimEnd()}\n\n{InDevelopmentFooter}";

    public static string FormatOpen(int deadlineHourMsk)
    {
        return WithFooter(
            $"""
            Сегодняшний рынок открыт.
            Категория: {BettingMarket.CategoryTitle}
            Ставки до {deadlineHourMsk}:00 МСК.

            Ставьте на топ-1 в этой категории (только приоритетные игроки).
            """);
    }

    public static string FormatReminder(int deadlineHourMsk, int bank)
    {
        return WithFooter(
            $"""
            Рынок ещё открыт до {deadlineHourMsk}:00 МСК.
            Категория: {BettingMarket.CategoryTitle}
            Банк сейчас: {bank} таблов.
            """);
    }

    public static string FormatClose(int bank)
    {
        return WithFooter(
            $"""
            Ставки закрыты.
            Категория: {BettingMarket.CategoryTitle}
            Банк: {bank} таблов.
            Итог — по статистике игрового дня.
            """);
    }

    public static string FormatGameStartedClose(int bank)
    {
        return WithFooter(
            $"""
            Окно ставок закрыто автоматически — игра началась.
            На сервере появились приоритетные игроки.
            Категория: {BettingMarket.CategoryTitle}
            Банк: {bank} таблов.
            Итог — по статистике игрового дня.
            """);
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

        return WithFooter(
            $"""
            Итог рынка: {BettingMarket.CategoryTitle}
            Победитель статистики: {top}

            Выиграли ставки:
            {winners.ToString().TrimEnd()}

            Банк {bank} таблов распределён.
            """);
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

        return WithFooter(
            $"""
            Рынок отменён. Ставки возвращены.
            Категория: {BettingMarket.CategoryTitle}
            Причина: {reasonText}
            Банк (возврат): {bank} таблов.
            """);
    }
}
