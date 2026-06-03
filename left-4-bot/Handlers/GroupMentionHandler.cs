using left_4_bot.L4D2;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public sealed class GroupMentionHandler
{
    public async Task HandleAsync(
        ITelegramBotClient bot,
        Message message,
        string command,
        CancellationToken cancellationToken)
    {
        switch (command.ToLowerInvariant())
        {
            case "random map":
                await HandleRandomMapAsync(bot, message, cancellationToken);
                break;
            default:
                break;
        }
    }

    private static async Task HandleRandomMapAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        var campaign = RandomCampaignPicker.Pick();

        await bot.SendMessage(
            message.Chat.Id,
            campaign,
            cancellationToken: cancellationToken);
    }
}