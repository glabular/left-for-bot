namespace left_4_bot.Persistence;

/// <summary>
/// Player-facing FAQ for <c>/bet FAQ</c>.
/// </summary>
public static class BetFaqText
{
    private const string InDevelopmentFooter = "⚠️ Тотализатор в разработке";

    /// <summary>
    /// Builds the FAQ body. Numbers come from <see cref="ToteEconomy"/>.
    /// </summary>
    public static string Build()
    {
        var minBet = ToteEconomy.MinBetTabs;
        var minBettors = ToteEconomy.MinBettorsPerMarket;
        var starter = ToteEconomy.StarterTabs;
        var category = BettingMarket.CategoryTitle;

        return
            $"""
            FAQ тотализатора

            ── Суть ──

            Q: Как устроены ставки?            
            A: Ставите виртуальные 💊 Таблы на исход дня. Все ставки — общий банк; угадавшие делят его пропорционально своим суммам (pari-mutuel). Бот не играет против вас и не даёт фиксированный коэффициент.

            Q: На что можно ставить?
            A: Пока только на топ-1 по «{category}» за игровой день.

            ── Участие ──

            Q: Как начать?
            A: Вызвать команду /balance (активировать тотализатор и разово получить {starter} таблов). Далее - посмотреть рынок через /bet.

            Q: Как ещё заработать Таблы?
            A: Пользуйтесь ботом ;) За вызов некоторых команд можно получить Таблы.

            Q: Правила ставки?
            A: Минимум {minBet} таблов, максимум — весь баланс. Одна ставка на день (цель + сумма); до закрытия окна можно заменить или отменить ставку.

            ── Выплаты ──

            Q: Как считается выигрыш?
            A: Банк делят угадавшие топ-1 пропорционально ставкам.

            Q: Когда ставки возвращают?
            A: Если ставящих меньше {minBettors}, никто не угадал топ-1, или рынок не смог посчитаться из‑за логов.

            Q: Когда итог?
            A: После смены игрового дня (~6:00 МСК), когда есть статистика.

            {InDevelopmentFooter}
            """;
    }
}
