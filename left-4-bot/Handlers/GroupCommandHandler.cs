using left_4_bot.L4D2;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public sealed class GroupCommandHandler
{
    private readonly GoldenPoolService _goldenPool;

    public GroupCommandHandler(GoldenPoolService goldenPool)
    {
        _goldenPool = goldenPool;
    }

    public async Task HandleAsync(
        ITelegramBotClient bot,
        Message message,
        string command,
        string args,
        CancellationToken cancellationToken)
    {
        switch (command.ToLowerInvariant())
        {
            case "map":
                await HandleGoldenMapAsync(bot, message, cancellationToken);
                break;

            case "addmap":
                await HandleAddMapAsync(bot, message, args, cancellationToken);
                break;

            case "removemap":
                await HandleRemoveMapAsync(bot, message, args, cancellationToken);
                break;

            case "randommap":
                await HandleRandomMapAsync(bot, message, cancellationToken);
                break;

            default:
                break;
        }
    }

    private async Task HandleGoldenMapAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        var campaign = GoldenPoolPicker.Pick(_goldenPool);

        var text = campaign is null
            ? "Golden pool is empty. Use /addmap <English name>."
            : campaign.Value.DisplayName;

        await bot.SendMessage(message.Chat.Id, text, cancellationToken: cancellationToken);
    }

    private async Task HandleAddMapAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Usage: /addmap <English name>  e.g. /addmap Dark Carnival",
                cancellationToken: cancellationToken);
            return;
        }

        var result = _goldenPool.TryAdd(args);

        var text = result switch
        {
            GoldenPoolService.AddResult.Added when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Added to golden pool: {c.DisplayName}",
            GoldenPoolService.AddResult.AlreadyExists when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Already in golden pool: {c.DisplayName}",
            GoldenPoolService.AddResult.UnknownCampaign
                => $"Unknown campaign: {args}",
            GoldenPoolService.AddResult.SaveFailed
                => "Failed to save golden pool to appsettings.json.",
            _ => "Could not add campaign."
        };

        await bot.SendMessage(message.Chat.Id, text, cancellationToken: cancellationToken);
    }

    private async Task HandleRemoveMapAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Usage: /removemap <English name>  e.g. /removemap Dark Carnival",
                cancellationToken: cancellationToken);
            return;
        }

        var result = _goldenPool.TryRemove(args);

        var text = result switch
        {
            GoldenPoolService.RemoveResult.Removed when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Removed from golden pool: {c.DisplayName}",
            GoldenPoolService.RemoveResult.NotInPool when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Not in golden pool: {c.DisplayName}",
            GoldenPoolService.RemoveResult.UnknownCampaign
                => $"Unknown campaign: {args}",
            GoldenPoolService.RemoveResult.SaveFailed
                => "Failed to save golden pool to appsettings.json.",
            _ => "Could not remove campaign."
        };

        await bot.SendMessage(message.Chat.Id, text, cancellationToken: cancellationToken);
    }

    private static async Task HandleRandomMapAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        var campaign = RandomCampaignPicker.Pick();

        await bot.SendMessage(
            message.Chat.Id,
            campaign.DisplayName,
            cancellationToken: cancellationToken);
    }
}