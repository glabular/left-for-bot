using left_4_bot.Persistence;
using left_4_bot.ServerLogs;

namespace left_4_bot.Spy;

/// <summary>
/// Consumes shared log-tail slices and queues enemy-facing team chat for Telegram when spy is enabled.
/// </summary>
public sealed class SpyLogTailConsumer : IServerLogTailConsumer
{
    private const string SpyPrefix = "\U0001f575\u200d\u2642\ufe0f"; // 🕵️‍

    private readonly SpyState _state;
    private readonly SpyForwarder _forwarder;
    private readonly PlayersService _playersService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpyLogTailConsumer"/> class.
    /// </summary>
    /// <param name="state">Shared spy enabled state.</param>
    /// <param name="forwarder">Queue that delivers chat lines to Telegram.</param>
    /// <param name="playersService">Players table used to skip priority players in the feed.</param>
    public SpyLogTailConsumer(
        SpyState state,
        SpyForwarder forwarder,
        PlayersService playersService)
    {
        _state = state;
        _forwarder = forwarder;
        _playersService = playersService;
    }

    /// <inheritdoc />
    public bool WantsPolling => _state.Enabled;

    /// <inheritdoc />
    public Task OnNewSlicesAsync(
        IReadOnlyList<ServerLogTailSlice> slices,
        CancellationToken cancellationToken)
    {
        if (!_state.Enabled)
        {
            return Task.CompletedTask;
        }

        var priority = _playersService.GetPrioritySteamIds();

        foreach (var slice in slices)
        {
            var messages = SpyChatParser.Parse(slice.Text);

            foreach (var message in messages)
            {
                // Priority list should not appear in the spy feed.
                if (priority.Contains(message.ClassicSteamId))
                {
                    continue;
                }

                _forwarder.Enqueue($"{SpyPrefix}[{message.PlayerName}]: {message.Text}");
            }
        }

        return Task.CompletedTask;
    }
}
