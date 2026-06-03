using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public static class CommandParser
{
    public static bool TryGetCommand(Message message, string botUsername, out string command)
    {
        command = string.Empty;

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return false;
        }

        var text = message.Text.Trim();

        if (!text.StartsWith('/'))
        {
            return false;
        }

        text = text[1..];

        var spaceIndex = text.IndexOf(' ');
        var firstToken = (spaceIndex >= 0 ? text[..spaceIndex] : text).Trim();

        if (firstToken.Length == 0)
        {
            return false;
        }

        var atIndex = firstToken.IndexOf('@');

        if (atIndex >= 0)
        {
            var commandName = firstToken[..atIndex];
            var mentionedBot = firstToken[(atIndex + 1)..];

            if (!mentionedBot.Equals(botUsername, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            firstToken = commandName;
        }

        if (firstToken.Length == 0)
        {
            return false;
        }

        command = firstToken;

        return true;
    }
}
