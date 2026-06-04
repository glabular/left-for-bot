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
            ? "Золотой пул пуст.\n\nИспользуйте /addmap <название на английском>\n\nНапример:\n    /addmap Dark Carnival"
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
                "Использование: /addmap <название на английском>\n\nНапример:\n    /addmap Dark Carnival",
                cancellationToken: cancellationToken);
            return;
        }

        var result = _goldenPool.TryAdd(args);

        var text = result switch
        {
            GoldenPoolService.AddResult.Added when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Добавлено в золотой пул: {c.DisplayName}",
            GoldenPoolService.AddResult.AlreadyExists when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Уже в золотом пуле: {c.DisplayName}",
            GoldenPoolService.AddResult.UnknownCampaign
                => $"Неизвестная кампания: {args}",
            GoldenPoolService.AddResult.SaveFailed
                => "Не удалось сохранить золотой пул в appsettings.json.",
            _ => "Не удалось добавить кампанию."
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
                "Использование: /removemap <название на английском>\n\nНапример:\n    /removemap Dark Carnival",
                cancellationToken: cancellationToken);
            return;
        }

        var result = _goldenPool.TryRemove(args);

        var text = result switch
        {
            GoldenPoolService.RemoveResult.Removed when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Удалено из золотого пула: {c.DisplayName}",
            GoldenPoolService.RemoveResult.NotInPool when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Нет в золотом пуле: {c.DisplayName}",
            GoldenPoolService.RemoveResult.UnknownCampaign
                => $"Неизвестная кампания: {args}",
            GoldenPoolService.RemoveResult.SaveFailed
                => "Не удалось сохранить золотой пул в appsettings.json.",
            _ => "Не удалось удалить кампанию."
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