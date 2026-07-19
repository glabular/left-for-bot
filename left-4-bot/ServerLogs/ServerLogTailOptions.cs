namespace left_4_bot.ServerLogs;

/// <summary>
/// Configuration options for the shared server log tail poller.
/// </summary>
public sealed class ServerLogTailOptions
{
    /// <summary>
    /// Configuration section name for log-tail options.
    /// </summary>
    public const string SectionName = "ServerLogTail";

    /// <summary>
    /// Gets or sets the log poll interval in seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 10;
}
