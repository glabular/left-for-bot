using left_4_bot.L4D2;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public sealed class GroupCommandHandler
{
    private const string HelpText =
        """
        Команды:
        /map — случайная карта из любимых
        /favmaps — список любимых карт
        /addmap <англ.> — добавить в любимые
        /removemap <англ.> — удалить из любимых
        /randommap — случайная карта из всех
        /help — справка

        Названия кампаний — на английском (например: Dark Carnival)
        """;

    private static string EditLimitExceededText =>
        $"Превышен дневной лимит изменений золотого пула ({GoldenPoolEditLimiter.DailyLimit} в день). "
        + "Слишком много правок списка сегодня — попробуйте завтра.";

    private readonly GoldenPoolService _goldenPool;
    private readonly GoldenPoolEditLimiter _editLimiter;

    public GroupCommandHandler(GoldenPoolService goldenPool, GoldenPoolEditLimiter editLimiter)
    {
        _goldenPool = goldenPool;
        _editLimiter = editLimiter;
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

            case "favmaps":
                await HandleFavMapsAsync(bot, message, cancellationToken);
                break;

            case "help":
                await HandleHelpAsync(bot, message, cancellationToken);
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

    private async Task HandleFavMapsAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        var campaigns = _goldenPool.GetCampaigns();

        if (campaigns.Count == 0)
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Золотой пул пуст.\n\nИспользуйте /addmap <название на английском>\n\nНапример:\n    /addmap Dark Carnival",
                cancellationToken: cancellationToken);
            return;
        }

        var lines = new List<string> { $"Золотой пул ({campaigns.Count}):" };

        for (var i = 0; i < campaigns.Count; i++)
        {
            lines.Add($"{i + 1}. {campaigns[i].DisplayName}");
        }

        await bot.SendMessage(
            message.Chat.Id,
            string.Join('\n', lines),
            cancellationToken: cancellationToken);
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

        var (allowed, result) = await TryApplyPoolEditAsync(
            bot,
            message,
            () => _goldenPool.TryAdd(args),
            r => r == GoldenPoolService.AddResult.Added,
            cancellationToken);

        if (!allowed)
        {
            return;
        }

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

        var (allowed, result) = await TryApplyPoolEditAsync(
            bot,
            message,
            () => _goldenPool.TryRemove(args),
            r => r == GoldenPoolService.RemoveResult.Removed,
            cancellationToken);

        if (!allowed)
        {
            return;
        }

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

    private static async Task HandleHelpAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            message.Chat.Id,
            HelpText,
            cancellationToken: cancellationToken);
    }

    private async Task<(bool Allowed, T Result)> TryApplyPoolEditAsync<T>(
        ITelegramBotClient bot,
        Message message,
        Func<T> applyEdit,
        Func<T, bool> editSucceeded,
        CancellationToken cancellationToken)
    {
        if (message.From?.Id is not { } userId)
        {
            return (true, applyEdit());
        }

        T result = default!;
        if (!_editLimiter.TryApplyEdit(userId, () =>
        {
            result = applyEdit();
            return editSucceeded(result);
        }))
        {
            await bot.SendMessage(message.Chat.Id, EditLimitExceededText, cancellationToken: cancellationToken);
            return (false, default!);
        }

        return (true, result);
    }
}