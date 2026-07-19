using left_4_bot.Call;
using left_4_bot.L4D2;
using left_4_bot.Rcon;
using left_4_bot.ServerLogs;
using left_4_bot.Spy;
using left_4_bot.Whois;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Handlers;

public sealed class GroupCommandHandler
{
    private const string HelpText =
        """
        <b><u>Что умеет этот бот</u></b>

        🗺️ <b>Карты</b>
        /map — случайная карта из любимых.
        /map random | /map r — случайная карта из всех официальных.
        /favmaps — список любимых карт.
        /favmaps &lt;англ.&gt; — добавить или удалить карту из любимых.
        Названия кампаний — на английском (например: Dark Carnival).

        📣 <b>Созыв игроков</b>
        /call — созвать игроков из списка.
        /call @юзернейм — добавить или удалить ник из списка.
        /call "Го играть!" — добавить или удалить фразу созыва.
        Ответьте /call на сообщение человека — добавить/удалить если у него нет @юзернейма.

        📊 <b>Статистика</b>
        /stat — статистика за текущий день.
        /stat [дата] — статистика за указанный день.

        🖥️ <b>Сервер</b>
        /status — статус сервера.
        /restart да | /restart yes — перезагрузить сервер.
        /server — подсказка как прописать сервер в игре.

        🔎 <b>Проверка игроков</b>
        /whois — включить или выключить автопроверку.
        При включении бот автоматически пишет в чат о новых игроках, их часах в игре и наличии VAC-блокировок.
        /whois now — проверить всех, кто сейчас на сервере.
        /whois status — показать, включена ли сейчас автопроверка.

        🕵️‍♂️ <b>Шпион</b>
        /spy — включить или выключить пересылку командного чата.
        /spy on | /spy off — явно включить или выключить.
        /spy status — показать, включён ли сейчас шпион.
        Пересылается только командный чат; сообщения приоритетных игроков игнорируются.
        В начале игрового дня шпион сам выключается.

        ℹ️ <b>Справка</b>
        /help — эта справка.
        """;

    private const string WhoisUsageText =
        """
        Использование:
            /whois — включить или выключить автопроверку
            /whois now — проверить игроков, которые сейчас на сервере
            /whois status — показать текущий статус автопроверки
        """;

    private const string SpyUsageText =
        """
        Использование:
            /spy — включить или выключить пересылку командного чата
            /spy on — включить
            /spy off — выключить
            /spy status — показать текущий статус

        Бот пересылает только командный чат (не общий).
        Сообщения приоритетных игроков не показываются.
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

    private const string MapUsageText =
        """
        Использование:
            /map — случайная карта из любимых
            /map random | /map r — случайная карта из всех официальных
        """;

    private const string EmptyPoolText =
        "Золотой пул пуст.\n\nИспользуйте /favmaps <название на английском>\n\nНапример:\n    /favmaps Dark Carnival";

    private const string EmptyCallListText =
        """
        Список пуст.

        Добавить с ником:
            /call @ник
        Без ника — ответьте /call на сообщение человека
        """;

    private const string EmptyCallPhrasesText =
        """
        Нет фраз для созыва.

        Добавьте фразу:
            /call "Го играть!"
        """;

    private const string CallUsageText =
        """
        Использование:
            /call — созвать список
            /call @ник — добавить или удалить ник
            /call "фраза" — добавить или удалить фразу
            ответьте /call на сообщение — добавить/удалить без ника
        """;

    private const string CallPhraseUsageText =
        """
        Фраза должна быть в двойных кавычках.

        Например:
            /call "Го играть!"
        """;

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
    private readonly CallListService _callList;
    private readonly PlayStatsService _playStats;
    private readonly ServerRconService _serverRcon;
    private readonly ServerRestartService _restartService;
    private readonly WhoisState _whoisState;
    private readonly WhoisService _whoisService;
    private readonly WhoisSessionTracker _whoisSessionTracker;
    private readonly SpyState _spyState;

    public GroupCommandHandler(
        GoldenPoolService goldenPool,
        GoldenPoolEditLimiter editLimiter,
        CallListService callList,
        PlayStatsService playStats,
        ServerRconService serverRcon,
        ServerRestartService restartService,
        WhoisState whoisState,
        WhoisService whoisService,
        WhoisSessionTracker whoisSessionTracker,
        SpyState spyState)
    {
        _goldenPool = goldenPool;
        _editLimiter = editLimiter;
        _callList = callList;
        _playStats = playStats;
        _serverRcon = serverRcon;
        _restartService = restartService;
        _whoisState = whoisState;
        _whoisService = whoisService;
        _whoisSessionTracker = whoisSessionTracker;
        _spyState = spyState;
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
                await HandleMapAsync(bot, message, args, cancellationToken);
                break;

            case "favmaps":
                await HandleFavMapsAsync(bot, message, args, cancellationToken);
                break;

            case "call":
                await HandleCallAsync(bot, message, args, cancellationToken);
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

            case "whois":
                await HandleWhoisAsync(bot, message, args, cancellationToken);
                break;

            case "spy":
                await HandleSpyAsync(bot, message, args, cancellationToken);
                break;

            default:
                break;
        }
    }

    private async Task HandleCallAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        var repliedUser = message.ReplyToMessage?.From;

        if (string.IsNullOrWhiteSpace(args))
        {
            if (repliedUser is not null && !repliedUser.IsBot)
            {
                await ToggleCallListEntryFromReplyAsync(bot, message, repliedUser, cancellationToken);
                return;
            }

            var phrase = _callList.PickRandomCallPhrase();
            if (phrase is null)
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    EmptyCallPhrasesText,
                    cancellationToken: cancellationToken);
                return;
            }

            var entries = _callList.GetCallListEntries();
            if (entries.Count == 0)
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    EmptyCallListText,
                    cancellationToken: cancellationToken);
                return;
            }

            var mentions = string.Join(' ', entries.Select(FormatMentionHtml));
            var text = $"{StatReportFormatting.EscapeHtml(phrase)}\n\n{mentions}";
            await bot.SendMessage(
                message.Chat.Id,
                text,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        if (CallListService.TryParseQuotedPhrase(args, out var phraseText, out var looksLikeQuoted))
        {
            var phraseResult = _callList.TryTogglePhrase(phraseText, out var phraseLabel);
            var phraseReply = phraseResult switch
            {
                CallToggleResult.Added => $"Добавлена фраза: {phraseLabel}",
                CallToggleResult.Removed => $"Удалена фраза: {phraseLabel}",
                CallToggleResult.Invalid => CallPhraseUsageText,
                CallToggleResult.SaveFailed => "Не удалось сохранить список в appsettings.Local.json.",
                _ => "Не удалось изменить список фраз.",
            };

            await bot.SendMessage(message.Chat.Id, phraseReply, cancellationToken: cancellationToken);
            return;
        }

        if (looksLikeQuoted)
        {
            await bot.SendMessage(
                message.Chat.Id,
                CallPhraseUsageText,
                cancellationToken: cancellationToken);
            return;
        }

        // Args without reply: toggle by @username only.
        var nickArg = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        if (!CallListService.TryNormalizeUsername(nickArg, out var username))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Некорректный ник. Telegram username: 5–32 символов (латиница, цифры, _).\n\n" + CallUsageText,
                cancellationToken: cancellationToken);
            return;
        }

        var result = _callList.TryToggleUsername(username);
        var textResult = result switch
        {
            CallToggleResult.Added => $"Добавлен: @{username}",
            CallToggleResult.Removed => $"Удалён: @{username}",
            CallToggleResult.Invalid =>
                "Некорректный ник. Telegram username: 5–32 символов (латиница, цифры, _).\n\n" + CallUsageText,
            CallToggleResult.SaveFailed => "Не удалось сохранить список в appsettings.Local.json.",
            _ => "Не удалось изменить список.",
        };

        await bot.SendMessage(message.Chat.Id, textResult, cancellationToken: cancellationToken);
    }

    private async Task ToggleCallListEntryFromReplyAsync(
        ITelegramBotClient bot,
        Message message,
        User repliedUser,
        CancellationToken cancellationToken)
    {
        // Prefer username toggle when the replied user has one — avoid id+nick duplicates.
        if (!string.IsNullOrWhiteSpace(repliedUser.Username))
        {
            var result = _callList.TryToggleUsername(repliedUser.Username);
            var text = result switch
            {
                CallToggleResult.Added => $"Добавлен: @{repliedUser.Username}",
                CallToggleResult.Removed => $"Удалён: @{repliedUser.Username}",
                CallToggleResult.SaveFailed => "Не удалось сохранить список в appsettings.Local.json.",
                _ => "Не удалось изменить список.",
            };

            await bot.SendMessage(message.Chat.Id, text, cancellationToken: cancellationToken);
            return;
        }

        var displayName = FormatTelegramDisplayName(repliedUser);
        var userResult = _callList.TryToggleUser(repliedUser.Id, displayName, out var label);
        var userText = userResult switch
        {
            CallToggleResult.Added => $"Добавлен: {label}",
            CallToggleResult.Removed => $"Удалён: {label}",
            CallToggleResult.Invalid => "Не удалось определить имя пользователя Telegram.",
            CallToggleResult.SaveFailed => "Не удалось сохранить список в appsettings.Local.json.",
            _ => "Не удалось изменить список.",
        };

        await bot.SendMessage(message.Chat.Id, userText, cancellationToken: cancellationToken);
    }

    private static string FormatTelegramDisplayName(User user)
    {
        if (string.IsNullOrWhiteSpace(user.LastName))
        {
            return user.FirstName;
        }

        return $"{user.FirstName} {user.LastName}";
    }

    private static string FormatMentionHtml(CallListEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.Username))
        {
            return "@" + entry.Username;
        }

        var id = entry.UserId
            ?? throw new InvalidOperationException("Call list entry has neither username nor user id.");
        var name = StatReportFormatting.EscapeHtml(entry.DisplayLabel);
        return $"<a href=\"tg://user?id={id}\">{name}</a>";
    }

    private async Task HandleMapAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            var favorite = RandomCampaignPicker.PickFrom(_goldenPool.GetCampaigns());

            var favoriteText = favorite is null
                ? EmptyPoolText
                : favorite.Value.DisplayName;

            await bot.SendMessage(message.Chat.Id, favoriteText, cancellationToken: cancellationToken);
            
            return;
        }

        var firstToken = args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];

        if (firstToken.Equals("random", StringComparison.OrdinalIgnoreCase)
            || firstToken.Equals("r", StringComparison.OrdinalIgnoreCase))
        {
            var campaign = RandomCampaignPicker.Pick();

            await bot.SendMessage(
                message.Chat.Id,
                campaign.DisplayName,
                cancellationToken: cancellationToken);

            return;
        }

        await bot.SendMessage(message.Chat.Id, MapUsageText, cancellationToken: cancellationToken);
    }

    private async Task HandleFavMapsAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(args))
        {
            var (allowed, result) = await TryEditGoldenPoolWithDailyLimitAsync(
                bot,
                message,
                () => _goldenPool.TryToggle(args),
                r => r is GoldenPoolToggleResult.Added or GoldenPoolToggleResult.Removed,
                cancellationToken);

            if (!allowed)
            {
                return;
            }

            var text = result switch
            {
                GoldenPoolToggleResult.Added when CampaignCatalog.TryGetByEnglishName(args, out var c)
                    => $"Добавлено в золотой пул: {c.DisplayName}",
                GoldenPoolToggleResult.Removed when CampaignCatalog.TryGetByEnglishName(args, out var c)
                    => $"Удалено из золотого пула: {c.DisplayName}",
                GoldenPoolToggleResult.UnknownCampaign
                    => $"Неизвестная кампания: {args}",
                GoldenPoolToggleResult.SaveFailed
                    => "Не удалось сохранить золотой пул в appsettings.Local.json.",
                _ => "Не удалось изменить золотой пул."
            };

            await bot.SendMessage(message.Chat.Id, text, cancellationToken: cancellationToken);

            return;
        }

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

    private async Task HandleStatAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        Task<PlayStatsResult> statsTask;

        if (string.IsNullOrWhiteSpace(args))
        {
            statsTask = _playStats.GetCurrentGamingDayStatsAsync(cancellationToken);
        }
        else
        {
            var today = DateOnly.FromDateTime(GamingDay.NowMoscow());
            if (!StatDateParser.TryParse(args, today, out var gamingDay))
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    """
                    Некорректная дата.

                    Напишите дату после команды. Варианты:
                    /stat 13 — 13 число текущего месяца
                    /stat 13.01 — 13 января текущего года
                    /stat 01.02.26 — 1 февраля 2026
                    /stat 01.02.2026 — полная дата

                    Без параметра — статистика за сегодня:
                    /stat
                    """,
                    cancellationToken: cancellationToken);

                return;
            }

            statsTask = _playStats.GetStatsForGamingDayAsync(gamingDay, cancellationToken);
        }

        await bot.SendMessage(
            message.Chat.Id,
            "Обрабатываю логи...",
            cancellationToken: cancellationToken);

        var result = await statsTask;

        await PlayStatsMessageSender.SendStatMessagesAsync(
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

        var begin = await _restartService.TryBeginServerRestartAsync(
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

    private async Task HandleWhoisAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        var trimmed = args.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            if (!_whoisState.Enabled && !_whoisService.HasApiKey)
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    WhoisService.MissingApiKeyMessage,
                    cancellationToken: cancellationToken);
                return;
            }

            var enabled = _whoisState.ToggleAutoCheckEnabled();
            var feedback = enabled
                ? "Проверка игроков включена.\nБот будет писать в группу при подключении новых игроков."
                : "Проверка игроков выключена.";

            await bot.SendMessage(
                message.Chat.Id,
                feedback,
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("now", StringComparison.OrdinalIgnoreCase))
        {
            await HandleWhoisNowAsync(bot, message, cancellationToken);
            return;
        }

        if (trimmed.Equals("status", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            var statusText = _whoisState.Enabled
                ? "\U0001f7e2 Статус команды whois: автопроверка заходов сейчас включена."
                : "⛔ Статус команды whois: автопроверка заходов сейчас выключена.";

            await bot.SendMessage(
                message.Chat.Id,
                statusText,
                cancellationToken: cancellationToken);
            return;
        }

        await bot.SendMessage(
            message.Chat.Id,
            WhoisUsageText,
            cancellationToken: cancellationToken);
    }

    private async Task HandleSpyAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        var trimmed = args.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            var enabled = _spyState.Toggle();
            var feedback = enabled
                ? "Шпион включён.\nБот будет беззвучно пересылать командный чат (кроме приоритетных игроков)."
                : "Шпион выключен.";

            await bot.SendMessage(
                message.Chat.Id,
                feedback,
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            _spyState.Set(true);
            await bot.SendMessage(
                message.Chat.Id,
                "Шпион включён.\nБот будет беззвучно пересылать командный чат (кроме приоритетных игроков).",
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            _spyState.Set(false);
            await bot.SendMessage(
                message.Chat.Id,
                "Шпион выключен.",
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("status", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            var statusText = _spyState.Enabled
                ? "\U0001f7e2 Статус команды spy: пересылка командного чата сейчас включена."
                : "⛔ Статус команды spy: пересылка командного чата сейчас выключена.";

            await bot.SendMessage(
                message.Chat.Id,
                statusText,
                cancellationToken: cancellationToken);
            return;
        }

        await bot.SendMessage(
            message.Chat.Id,
            SpyUsageText,
            cancellationToken: cancellationToken);
    }

    private async Task HandleWhoisNowAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        if (!_whoisService.HasApiKey)
        {
            await bot.SendMessage(
                message.Chat.Id,
                WhoisService.MissingApiKeyMessage,
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

        await bot.SendMessage(
            message.Chat.Id,
            "Проверяю игроков на сервере…",
            cancellationToken: cancellationToken);

        var result = await _serverRcon.TryExecuteAsync("status", cancellationToken);
        if (!result.IsSuccess)
        {
            await bot.SendMessage(
                message.Chat.Id,
                result.Message,
                cancellationToken: cancellationToken);
            return;
        }

        var players = RconStatusPlayerParser.Parse(result.Message);
        await _whoisService.SendWhoisReportAsync(
            bot,
            message.Chat.Id,
            players,
            cancellationToken);

        var announced = _whoisService.ExcludePriorityPlayers(players);
        _whoisSessionTracker.MarkAnnounced(announced.Select(p => p.ClassicSteamId));
    }

    private static async Task HandleHelpAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        await bot.SendMessage(
            message.Chat.Id,
            HelpText,
            parseMode: ParseMode.Html,
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

    private async Task<(bool Allowed, T Result)> TryEditGoldenPoolWithDailyLimitAsync<T>(
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
