using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public static class MentionCommandParser
{
    public static bool TryGetCommand(Message message, string botUsername, out string command)
    {
        command = string.Empty;

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return false;
        }

        var mention = $"@{botUsername}";
        var text = message.Text.Trim();

        if (!text.StartsWith(mention, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        command = text[mention.Length..].Trim();

        return command.Length > 0;
    }
}
