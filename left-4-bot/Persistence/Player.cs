namespace left_4_bot.Persistence;

public sealed class Player
{
    public long Id { get; set; }

    public string SteamId { get; set; } = string.Empty; // classic STEAM_X:Y:Z

    public long? TelegramUserId { get; set; }

    /// <summary>
    /// Public Telegram @username.
    /// </summary>
    public string? TelegramUsername { get; set; }

    public bool IsPriority { get; set; }

    public DateTimeOffset? LinkedAt { get; set; }

    public int Balance { get; set; }

    public DateTimeOffset? StarterGrantedAt { get; set; }
}
