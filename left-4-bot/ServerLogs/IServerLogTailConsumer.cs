namespace left_4_bot.ServerLogs;

/// <summary>
/// Receives near-real-time slices from active server logs downloaded by
/// <see cref="ServerLogTailHostedService"/>.
/// </summary>
public interface IServerLogTailConsumer
{
    /// <summary>
    /// Gets a value indicating whether this consumer currently needs the shared log tail to poll FTP.
    /// </summary>
    bool WantsPolling { get; }

    /// <summary>
    /// Handles newly appended log slices from the current poll tick.
    /// </summary>
    /// <param name="slices">Non-empty slices with new bytes since the previous poll.</param>
    /// <param name="cancellationToken">Token used to cancel processing.</param>
    Task OnNewSlicesAsync(IReadOnlyList<ServerLogTailSlice> slices, CancellationToken cancellationToken);
}
