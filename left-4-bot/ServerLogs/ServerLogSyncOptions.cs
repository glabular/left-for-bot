namespace left_4_bot.ServerLogs;

public sealed class ServerLogSyncOptions
{
    public const string SectionName = "ServerLogs";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 21;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string RemoteLogDirectory { get; set; } = "logs";

    public string LocalInboxDirectory { get; set; } = "data/server-logs/inbox";
}
