using left_4_bot.ServerLogs;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Handlers;

public static class PlayStatsMessageSender
{
    private static readonly TimeSpan CategorySendDelay = TimeSpan.FromSeconds(1);

    public static async Task SendStatMessagesAsync(
        ITelegramBotClient bot,
        ChatId chatId,
        IReadOnlyList<PlayStatsOutboundMessage> messages,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < messages.Count; i++)
        {
            if (i > 0 && messages[i].DisableNotification)
            {
                await Task.Delay(CategorySendDelay, cancellationToken);
            }

            var msg = messages[i];
            await bot.SendMessage(
                chatId,
                msg.Text,
                parseMode: msg.UseHtml ? ParseMode.Html : default,
                disableNotification: msg.DisableNotification,
                cancellationToken: cancellationToken);
        }
    }
}
