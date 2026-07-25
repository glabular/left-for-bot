using System.Collections.Generic;

namespace left_4_bot.Persistence;

/// <summary>
/// In-memory bets for the current gaming day. Lost on process restart (EF persistence is a later step).
/// </summary>
public sealed class InMemoryDailyBetStore
{
    private readonly object _gate = new();
    private DateOnly? _anchor;
    private readonly Dictionary<long, DailyBet> _betsByTelegramUserId = new();

    public DailyBet? TryGet(DateOnly gamingDayAnchor, long telegramUserId)
    {
        lock (_gate)
        {
            EnsureDayUnlocked(gamingDayAnchor);
            return _betsByTelegramUserId.TryGetValue(telegramUserId, out var bet) ? bet : null;
        }
    }

    public void Set(DailyBet bet)
    {
        lock (_gate)
        {
            EnsureDayUnlocked(bet.GamingDayAnchor);
            _betsByTelegramUserId[bet.TelegramUserId] = bet;
        }
    }

    public bool TryRemove(DateOnly gamingDayAnchor, long telegramUserId, out DailyBet? removed)
    {
        lock (_gate)
        {
            EnsureDayUnlocked(gamingDayAnchor);

            if (_betsByTelegramUserId.Remove(telegramUserId, out var bet))
            {
                removed = bet;
                return true;
            }

            removed = null;
            return false;
        }
    }

    private void EnsureDayUnlocked(DateOnly gamingDayAnchor)
    {
        if (_anchor == gamingDayAnchor)
        {
            return;
        }

        _betsByTelegramUserId.Clear();
        _anchor = gamingDayAnchor;
    }
}
