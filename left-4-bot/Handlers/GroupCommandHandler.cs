using System.Globalization;
using left_4_bot.L4D2;
using left_4_bot.Rcon;
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

        /stat [дд.мм.гггг] — статистика за день (без даты — текущий)

        /status — статус сервера
        /restart да|yes — перезагрузить сервер
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

    private const string RestartInstructionText =
        """
        Во избежание случайной перезагрузки команде требуется параметр подтвержения.

        Чтобы перезагрузить сервер, отправьте:
            /restart да
        или
            /restart yes

        Команда /restart без параметра сервер не перезагружает.
        """;

    private readonly GoldenPoolService _goldenPool;
    private readonly GoldenPoolEditLimiter _editLimiter;
    private readonly PlayStatsService _playStats;
    private readonly ServerRconService _serverRcon;
    private readonly ServerRestartService _restartService;

    public GroupCommandHandler(
        GoldenPoolService goldenPool,
        GoldenPoolEditLimiter editLimiter,
        PlayStatsService playStats,
        ServerRconService serverRcon,
        ServerRestartService restartService)
    {
        _goldenPool = goldenPool;
        _editLimiter = editLimiter;
        _playStats = playStats;
        _serverRcon = serverRcon;
        _restartService = restartService;
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

            case "stat":
                await HandleStatAsync(bot, message, args, cancellationToken);
                break;

            case "status":
                await HandleStatusAsync(bot, message, cancellationToken);
                break;

            case "restart":
                await HandleRestartAsync(bot, message, args, cancellationToken);
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

    private async Task HandleStatAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        Task<PlayStatsResult> statsTask;

        if (string.IsNullOrWhiteSpace(args))
        {
            statsTask = _playStats.GetTodayStatsAsync(cancellationToken);
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
                "Некорректная дата.\nТребуется формат /stat [дд.мм.гггг]  " +
                "\n\nНапример\n/stat 01.02.2026\nИли без параметра, чтобы получить статистику за сегодня\n/stat",
                cancellationToken: cancellationToken);
            return;
        }
        else
        {
            statsTask = _playStats.GetStatsForGamingDayAsync(gamingDay, cancellationToken);
        }

        await bot.SendMessage(
            message.Chat.Id,
            "Обрабатываю логи...",
            cancellationToken: cancellationToken);

        var result = await statsTask;

        await PlayStatsMessageSender.SendAsync(
            bot,
            message.Chat.Id,
            result.Messages,
            cancellationToken);
    }

    private async Task HandleRestartAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            await bot.SendMessage(
                message.Chat.Id,
                RestartInstructionText,
                cancellationToken: cancellationToken);
            return;
        }

        if (!IsRestartConfirmation(args))
        {
            await bot.SendMessage(
                message.Chat.Id,
                $"Неверный параметр.\n\n{RestartInstructionText}",
                cancellationToken: cancellationToken);
            return;
        }

        var configError = _serverRcon.GetConfigurationError();
        if (configError is not null)
        {
            await bot.SendMessage(
                message.Chat.Id,
                configError,
                cancellationToken: cancellationToken);
            return;
        }

        var begin = await _restartService.TrySendQuitAsync(
            message.From?.Id,
            message.From?.Username,
            cancellationToken);

        if (begin is not null)
        {
            await bot.SendMessage(
                message.Chat.Id,
                begin.UserMessage,
                cancellationToken: cancellationToken);
            return;
        }

        await bot.SendMessage(
            message.Chat.Id,
            "Команда перезагрузки успешно отправлена, жду поднятия сервера…",
            cancellationToken: cancellationToken);

        var chatId = message.Chat.Id;
        _ = WaitForRestartAndNotifyAsync(bot, chatId, cancellationToken);
    }

    private async Task WaitForRestartAndNotifyAsync(
        ITelegramBotClient bot,
        ChatId chatId,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _restartService.WaitForServerOnlineAsync(cancellationToken);

            await bot.SendMessage(
                chatId,
                outcome.UserMessage,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await bot.SendMessage(
                chatId,
                "Не удалось дождаться поднятия сервера.",
                cancellationToken: CancellationToken.None);
        }
    }

    private static bool IsRestartConfirmation(string args) =>
        args.Trim().Equals("да", StringComparison.OrdinalIgnoreCase)
        || args.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);

    private async Task HandleStatusAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            message.Chat.Id,
            "Получаю статус сервера…",
            cancellationToken: cancellationToken);

        var result = await _serverRcon.GetStatusAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                cancellationToken: cancellationToken);
            return;
        }

        var text = $"<pre>{StatReportFormatting.EscapeHtml(result.Message)}</pre>";

        await bot.SendMessage(
            message.Chat.Id,
            text,
            parseMode: ParseMode.Html,
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