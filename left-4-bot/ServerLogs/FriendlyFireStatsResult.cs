namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsResult
{
    public bool IsSuccess { get; init; }

    public bool UseHtmlParseMode { get; init; }

    public string Message { get; init; } = "";

    public static FriendlyFireStatsResult Succeeded(string message, bool useHtmlParseMode = false) =>
        new()
        {
            IsSuccess = true,
            Message = message,
            UseHtmlParseMode = useHtmlParseMode,
        };

    public static FriendlyFireStatsResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
        };
}
