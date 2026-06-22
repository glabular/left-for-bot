using System.Globalization;
using left_4_bot.L4D2;
using left_4_bot.ServerLogs;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Handlers;

public sealed class GroupCommandHandler
{
    private const string HelpText =
        """
        Команды:
        /map — случайная карта из любимых
        /randommap — случайная карта из всех

        /favmaps — список любимых карт
        /addmap <англ.> — добавить в любимые
        /removemap <англ.> — удалить из любимых        
        Названия кампаний — на английском (например: Dark Carnival)

        /ff [дд.мм.гггг] — дружественный огонь (без даты — текущий день)

        /stat — убийства обычных заражённых за текущий день

        /server — как прописать сервер в игре
        /help — справка
        """;

    private const string ServerSetupText =
        """
        <b>Как прописать сервер</b>

        1. Открыть консоль в игре (клавиша тильда, <b>~</b>)
        2. Ввести команду и нажать <b>Enter</b>:
            <code>mm_dedicated_force_servers 46.174.48.12:27217</code>
        3. В настройках лобби выбрать лучший доступный выделенный сервер.
        """;

    private static string EditLimitExceededText =>
        $"Превышен дневной лимит изменений золотого пула ({GoldenPoolEditLimiter.DailyLimit} в день). "
        + "Слишком много правок списка сегодня — попробуйте завтра.";

    private const string EmptyPoolText =
        "Золотой пул пуст.\n\nИспользуйте /addmap <название на английском>\n\nНапример:\n    /addmap Dark Carnival";

    private readonly GoldenPoolService _goldenPool;
    private readonly GoldenPoolEditLimiter _editLimiter;
    private readonly FriendlyFireStatsService _friendlyFireStats;
    private readonly CommonKillsStatsService _commonKillsStats;

    public GroupCommandHandler(
        GoldenPoolService goldenPool,
        GoldenPoolEditLimiter editLimiter,
        FriendlyFireStatsService friendlyFireStats,
        CommonKillsStatsService commonKillsStats)
    {
        _goldenPool = goldenPool;
        _editLimiter = editLimiter;
        _friendlyFireStats = friendlyFireStats;
        _commonKillsStats = commonKillsStats;
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

            case "ff":
                await HandleFriendlyFireAsync(bot, message, args, cancellationToken);
                break;

            case "stat":
                await HandleStatAsync(bot, message, cancellationToken);
                break;

            case "server":
                await HandleServerSetupAsync(bot, message, cancellationToken);
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
        var campaign = RandomCampaignPicker.PickFrom(_goldenPool.GetCampaigns());

        var text = campaign is null
            ? EmptyPoolText
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
                EmptyPoolText,
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
            r => r == AddResult.Added,
            cancellationToken);

        if (!allowed)
        {
            return;
        }

        var text = result switch
        {
            AddResult.Added when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Добавлено в золотой пул: {c.DisplayName}",
            AddResult.AlreadyExists when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Уже в золотом пуле: {c.DisplayName}",
            AddResult.UnknownCampaign
                => $"Неизвестная кампания: {args}",
            AddResult.SaveFailed
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
            r => r == RemoveResult.Removed,
            cancellationToken);

        if (!allowed)
        {
            return;
        }

        var text = result switch
        {
            RemoveResult.Removed when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Удалено из золотого пула: {c.DisplayName}",
            RemoveResult.NotInPool when CampaignCatalog.TryGetByEnglishName(args, out var c)
                => $"Нет в золотом пуле: {c.DisplayName}",
            RemoveResult.UnknownCampaign
                => $"Неизвестная кампания: {args}",
            RemoveResult.SaveFailed
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

    private async Task HandleFriendlyFireAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        Task<FriendlyFireStatsResult> statsTask;

        if (string.IsNullOrWhiteSpace(args))
        {
            statsTask = _friendlyFireStats.GetTodayStatsAsync(cancellationToken);
        }
        else if (!DateOnly.TryParseExact(
            args,
            "dd.MM.yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var gamingDay))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Некорректная дата.\nТребуется формат /ff [дд.мм.гггг]  " +
                "\n\nНапример\n/ff 01.02.2026\nИли без параметра, чтобы получить статистику за сегодня\n/ff",
                cancellationToken: cancellationToken);
            return;
        }
        else
        {
            statsTask = _friendlyFireStats.GetStatsForGamingDayAsync(gamingDay, cancellationToken);
        }

        await bot.SendMessage(
            message.Chat.Id,
            "Обрабатываю логи...",
            cancellationToken: cancellationToken);

        var result = await statsTask;

        if (result.UseHtmlParseMode)
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStatAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            message.Chat.Id,
            "Обрабатываю логи...",
            cancellationToken: cancellationToken);

        var result = await _commonKillsStats.GetTodayStatsAsync(cancellationToken);

        if (result.UseHtmlParseMode)
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                cancellationToken: cancellationToken);
        }
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

    private static async Task HandleServerSetupAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            message.Chat.Id,
            ServerSetupText,
            parseMode: ParseMode.Html,
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