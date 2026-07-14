namespace left_4_bot.Call;

public sealed class CallListOptions
{
    public const string SectionName = "CallList";

    public List<string> Usernames { get; set; } = [];

    public List<CallListUser> Users { get; set; } = [];

    public List<string> Phrases { get; set; } = [];
}
