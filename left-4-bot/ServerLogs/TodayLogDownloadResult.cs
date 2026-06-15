namespace left_4_bot.ServerLogs;

public sealed class TodayLogDownloadResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = "";

    public IReadOnlyList<string> LocalFilePaths { get; init; } = [];

    public static TodayLogDownloadResult Succeeded(
        IReadOnlyList<string> localFilePaths,
        string message) =>
        new()
        {
            IsSuccess = true,
            LocalFilePaths = localFilePaths,
            Message = message,
        };

    public static TodayLogDownloadResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
        };
}
