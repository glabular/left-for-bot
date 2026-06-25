namespace left_4_bot.ServerLogs;

public sealed class PlayStatsOutboundMessage
{
    public required string Text { get; init; }

    public bool UseHtml { get; init; }

    public bool DisableNotification { get; init; }
}

public sealed class PlayStatsResult
{
    public bool IsSuccess { get; init; }

    public IReadOnlyList<PlayStatsOutboundMessage> Messages { get; init; } = [];

    public static PlayStatsResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Messages =
            [
                new PlayStatsOutboundMessage { Text = message },
            ],
        };

    public static PlayStatsResult Succeeded(IReadOnlyList<PlayStatsOutboundMessage> messages) =>
        new()
        {
            IsSuccess = true,
            Messages = messages,
        };
}
