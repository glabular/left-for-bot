using Telegram.Bot.Types;

namespace left_4_bot.TelegramClient;

/// <summary>
/// Keeps outbound Telegram chat requests under Bot API flood limits.
/// </summary>
public sealed class TelegramSendRateLimiter
{
    // Telegram allows about 1 message per second to the same chat.
    private static readonly TimeSpan PerChatMinInterval = TimeSpan.FromSeconds(1);

    // Global bot limit is ~30/sec; stay a bit under that (~25/sec).
    private static readonly TimeSpan GlobalMinInterval = TimeSpan.FromMilliseconds(40);

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _nextChatReady = new(StringComparer.Ordinal);
    private DateTimeOffset _nextGlobalReady = DateTimeOffset.MinValue;

    public Task WaitAsync(ChatId chatId, CancellationToken cancellationToken)
    {
        TimeSpan delay;

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var chatKey = chatId.ToString();

            // Pick the latest of "now", per-chat slot, and global slot.
            var readyAt = now;

            if (_nextChatReady.TryGetValue(chatKey, out var chatReady) && chatReady > readyAt)
            {
                readyAt = chatReady;
            }

            if (_nextGlobalReady > readyAt)
            {
                readyAt = _nextGlobalReady;
            }

            delay = readyAt - now;

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            // Reserve the slot now so parallel callers do not pick the same instant.
            _nextChatReady[chatKey] = readyAt + PerChatMinInterval;
            _nextGlobalReady = readyAt + GlobalMinInterval;
        }

        return delay > TimeSpan.Zero
            ? Task.Delay(delay, cancellationToken)
            : Task.CompletedTask;
    }
}
