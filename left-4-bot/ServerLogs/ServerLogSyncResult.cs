namespace left_4_bot.ServerLogs;

public sealed class ServerLogSyncResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = "";

    public string? LocalPath { get; init; }

    public string? RemoteFileName { get; init; }

    public static ServerLogSyncResult Succeeded(
        string localPath,
        string remoteFileName,
        string message) =>
        new()
        {
            IsSuccess = true,
            LocalPath = localPath,
            RemoteFileName = remoteFileName,
            Message = message,
        };

    public static ServerLogSyncResult Failed(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message,
        };
}
