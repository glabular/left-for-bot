using left_4_bot.ServerLogs;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Handlers;

public static class PlayStatsMessageSender
{
    // Spacing between messages is handled by ThrottlingTelegramBotClient (global send throttle).
    public static async Task SendStatMessagesAsync(
        ITelegramBotClient bot,
        ChatId chatId,
        IReadOnlyList<PlayStatsOutboundMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var msg in messages)
        {
            await bot.SendMessage(
                chatId,
                msg.Text,
                parseMode: msg.UseHtml ? ParseMode.Html : default,
                disableNotification: msg.DisableNotification,
                cancellationToken: cancellationToken);
        }
    }
}
