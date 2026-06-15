namespace left_4_bot.ServerLogs;

public sealed record RemoteFileInfo(
    string Name,
    string FullPath,
    long Size,
    DateTime Modified);
