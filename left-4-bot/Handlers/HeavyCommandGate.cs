namespace left_4_bot.Handlers;

/// <summary>
/// Serializes heavy Telegram commands (e.g. <c>/stat</c>) so only one runs at a time.
/// Callers that fail to enter should reject immediately rather than queue.
/// Always pair a successful enter with <c>try/finally { Exit(); }</c>.
/// </summary>
public sealed class HeavyCommandGate
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    /// <summary>
    /// Tries to acquire the gate without waiting.
    /// </summary>
    /// <param name="cancellationToken">Token passed to the non-blocking wait.</param>
    /// <returns>
    /// <see langword="true"/> if this caller holds the gate and must call <see cref="Exit"/>;
    /// <see langword="false"/> if another heavy command is already running.
    /// </returns>
    public async Task<bool> TryEnterAsync(CancellationToken cancellationToken)
    {
        return await _mutex.WaitAsync(0, cancellationToken);
    }

    /// <summary>
    /// Releases the gate. Call only after a successful <see cref="TryEnterAsync"/>.
    /// </summary>
    public void Exit()
    {
        _mutex.Release();
    }
}
