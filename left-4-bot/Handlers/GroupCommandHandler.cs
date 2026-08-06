using left_4_bot.Call;
using left_4_bot.L4D2;
using left_4_bot.Persistence;
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
        /whois on | /whois off — явно включить или выключить.
        При включении бот автоматически пишет в чат о новых игроках, их часах в игре и наличии VAC-блокировок (приоритетные игроки пропускаются).
        /whois now — проверить всех, кто сейчас на сервере, включая приоритетных игроков.
        /whois status — показать, включена ли сейчас автопроверка.

        🕵️‍♂️ <b>Шпион</b>
        /spy — включить или выключить пересылку командного чата.
        /spy on | /spy off — явно включить или выключить.
        /spy status — показать, включён ли сейчас шпион.
        Пересылается только командный чат; сообщения приоритетных игроков игнорируются.
        В начале игрового дня шпион сам выключается.

        🔗 <b>Привязка Steam</b>
        /link &lt;ссылка на профиль Steam&gt; — связать Telegram с Steam.

        ⚠️ <b>Тотализатор (в разработке)</b>
        /balance — баланс таблов.
        /bet — рынок (топ-1, Защита союзников): список целей и ставка.
        /bet FAQ — частые вопросы по тотализатору.

        ℹ️ <b>Справка</b>
        /help — эта справка.
        """;

    private const string WhoisUsageText =
        """
        Использование:
            /whois — включить или выключить автопроверку
            /whois on — включить
            /whois off — выключить
            /whois now — проверить всех на сервере, включая приоритетных игроков
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

    private const string LinkUsageText =
        """
        Использование:
            /link <ссылка на профиль Steam>

        Примеры:
            /link https://steamcommunity.com/profiles/76561198000000000
            /link https://steamcommunity.com/id/yourname
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
        Фраза должна быть в парных кавычках: "…", «…» или '…'.
        Внутри фразы кавычки можно использовать — важна только внешняя пара.

        Например:
            /call "Го играть!"
            /call "Есть "зачем" жить"
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
    private readonly PlayersService _playersService;
    private readonly SteamPlayerProfileService _steamProfiles;
    private readonly BettingService _bettingService;
    private readonly HeavyCommandGate _heavyCommandGate;

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
        SpyState spyState,
        PlayersService playersService,
        SteamPlayerProfileService steamProfiles,
        BettingService bettingService,
        HeavyCommandGate heavyCommandGate)
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
        _playersService = playersService;
        _steamProfiles = steamProfiles;
        _bettingService = bettingService;
        _heavyCommandGate = heavyCommandGate;
    }

    public async Task HandleAsync(
        ITelegramBotClient bot,
        Message message,
        string command,
        string args,
        CancellationToken cancellationToken)
    {
        var handled = true;

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
            case "link":
                await HandleLinkAsync(bot, message, args, cancellationToken);
                break;

            case "balance":
                await HandleBalanceAsync(bot, message, cancellationToken);
                break;

            case "bet":
                await HandleBetAsync(bot, message, args, cancellationToken);
                break;

            default:
                handled = false;
                break;
        }

        if (handled)
        {
            await TryGrantCommandRewardAndNotifyAsync(bot, message, command, args, cancellationToken);
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
            var invalidReply = CallListService.LooksLikeUsernameAttempt(args)
                ? "Некорректный ник. Telegram username: 5–32 символов (латиница, цифры, _).\n\n" + CallUsageText
                : CallUsageText;

            await bot.SendMessage(
                message.Chat.Id,
                invalidReply,
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
        DateOnly? gamingDay = null;

        if (!string.IsNullOrWhiteSpace(args))
        {
            var today = DateOnly.FromDateTime(GamingDay.NowMoscow());
            if (!StatDateParser.TryParse(args, today, out var parsedDay))
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

            gamingDay = parsedDay;
        }

        if (!await _heavyCommandGate.TryEnterAsync(cancellationToken))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "⚠️ Уже обрабатывается другая команда. Подождите окончания текущего запроса.",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            var statsTask = gamingDay is { } day
                ? _playStats.GetStatsForGamingDayAsync(day, cancellationToken)
                : _playStats.GetCurrentGamingDayStatsAsync(cancellationToken);

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
        finally
        {
            _heavyCommandGate.Exit();
        }
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

        if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            if (!_whoisService.HasApiKey)
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    WhoisService.MissingApiKeyMessage,
                    cancellationToken: cancellationToken);
                return;
            }

            _whoisState.Set(true);
            await bot.SendMessage(
                message.Chat.Id,
                "Проверка игроков включена.\nБот будет писать в группу при подключении новых игроков.",
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            _whoisState.Set(false);
            await bot.SendMessage(
                message.Chat.Id,
                "Проверка игроков выключена.",
                cancellationToken: cancellationToken);
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
            cancellationToken,
            includePriorityPlayers: true);

        // Auto-check never announces priority players, so only mark non-priority as announced.
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

    private async Task HandleBalanceAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        var result = await _playersService.GetOrClaimStarterBalanceAsync(
            message.From.Id,
            cancellationToken);

        var reply = result.Status switch
        {
            BalanceClaimStatus.NeedLink =>
                WithToteFooter(
                    """
                    Сначала привяжи Steam:
                        /link &lt;ссылка на профиль Steam&gt;
                    """),
            BalanceClaimStatus.Joined =>
                WithToteFooter(
                    $"""
                    🎉 Поздравляем! 🎉

                    На счёт зачислено {ToteEconomy.StarterTabs} таблов 💊 💊 💊 
                    💰 Текущий баланс: {result.Balance} таблов.
                    """),
            BalanceClaimStatus.AlreadyJoined =>
                WithToteFooter($"💰 Твой текущий баланс: {result.Balance} таблов."),
            _ => WithToteFooter("Не удалось получить баланс."),
        };

        await bot.SendMessage(
            message.Chat.Id,
            reply,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private const string BetMiniHelpText =
        """
        Как сделать ставку:
            /bet <номер> <сумма>
        Пример: /bet 3 100

        Отменить ставку:
            /bet cancel

        Частые вопросы:
            /bet FAQ
        """;

    private const string ToteInDevelopmentFooter = "⚠️ Тотализатор в разработке";

    private static string WithToteFooter(string body) =>
        $"{body.TrimEnd()}\n\n{ToteInDevelopmentFooter}";

    private static string BetDayIntroText =>
        $"Сейчас можно поставить таблы на то, кто займёт 1 место по «{BettingMarket.CategoryTitle}» за игровой день.";

    private async Task HandleBetAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        var trimmed = args.Trim();
        var telegramUserId = message.From.Id;

        if (string.IsNullOrEmpty(trimmed))
        {
            await SendBetListAsync(bot, message, telegramUserId, errorLine: null, cancellationToken);
            return;
        }

        if (trimmed.Equals("FAQ", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessage(
                message.Chat.Id,
                BetFaqText.Build(),
                cancellationToken: cancellationToken);
            return;
        }

        if (trimmed.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            var cancelResult = await _bettingService.CancelAsync(telegramUserId, cancellationToken);
            var cancelNames = await ResolveBetDisplayNamesAsync(cancelResult.PriorityTargets, cancellationToken);
            await bot.SendMessage(
                message.Chat.Id,
                FormatBetActionReply(cancelResult, cancelNames),
                cancellationToken: cancellationToken);
            return;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var index)
            || !int.TryParse(parts[1], out var amount))
        {
            await SendBetListAsync(
                bot,
                message,
                telegramUserId,
                errorLine: "Не понял формат. Нужны номер из списка и сумма таблов.",
                cancellationToken);
            return;
        }

        var result = await _bettingService.PlaceOrReplaceAsync(
            telegramUserId,
            index,
            amount,
            cancellationToken);

        var names = await ResolveBetDisplayNamesAsync(result.PriorityTargets, cancellationToken);
        await bot.SendMessage(
            message.Chat.Id,
            FormatBetActionReply(result, names),
            cancellationToken: cancellationToken);
    }

    private async Task SendBetListAsync(
        ITelegramBotClient bot,
        Message message,
        long telegramUserId,
        string? errorLine,
        CancellationToken cancellationToken)
    {
        var targets = _bettingService.GetPriorityTargets();
        var current = await _bettingService.TryGetCurrentBetAsync(telegramUserId, cancellationToken);
        var names = await ResolveBetDisplayNamesAsync(targets, cancellationToken);
        var text = FormatBetListMessage(targets, current, errorLine, names);

        await bot.SendMessage(
            message.Chat.Id,
            text,
            cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveBetDisplayNamesAsync(
        IReadOnlyList<Player> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var personaBySteamId = await _steamProfiles.GetPersonaNamesByClassicSteamIdAsync(
            targets.Select(t => t.SteamId).ToList(),
            cancellationToken);

        var display = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var target in targets)
        {
            display[target.SteamId] = BuildBetDisplayName(target, personaBySteamId);
        }

        return display;
    }

    private static string BuildBetDisplayName(
        Player player,
        IReadOnlyDictionary<string, string> personaBySteamId)
    {
        if (personaBySteamId.TryGetValue(player.SteamId, out var personaName)
            && !string.IsNullOrWhiteSpace(personaName))
        {
            return personaName;
        }

        if (!string.IsNullOrWhiteSpace(player.TelegramUsername))
        {
            return player.TelegramUsername!;
        }

        return "Игрок без публичного ника";
    }

    private static string FormatBetListMessage(
        IReadOnlyList<Player> targets,
        DailyBet? currentBet,
        string? errorLine,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var builder = new System.Text.StringBuilder();

        // The market description belongs to the plain /bet help only, not to error replies.
        if (string.IsNullOrEmpty(errorLine))
        {
            builder.AppendLine(BetDayIntroText);
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine(errorLine);
            builder.AppendLine();
        }

        if (currentBet is not null)
        {
            var targetLabel = displayNames.TryGetValue(currentBet.TargetSteamId, out var name)
                ? name
                : "выбранный игрок";
            builder.AppendLine(
                $"Твоя ставка сейчас: {currentBet.Amount} таблов на {targetLabel} (1 место по «{BettingMarket.CategoryTitle}»).");
            builder.AppendLine();
        }

        if (targets.Count == 0)
        {
            builder.AppendLine("Пока некого выбрать: в приоритетном списке никого нет.");
        }
        else
        {
            builder.AppendLine("Выбери игрока по номеру:");
            for (var i = 0; i < targets.Count; i++)
            {
                builder.AppendLine(FormatPriorityTargetLine(i + 1, targets[i], displayNames));
            }
        }

        builder.AppendLine();
        builder.Append(BetMiniHelpText);
        builder.AppendLine();
        builder.AppendLine(ToteInDevelopmentFooter);
        return builder.ToString();
    }

    private static string FormatPriorityTargetLine(
        int number,
        Player player,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var label = displayNames.TryGetValue(player.SteamId, out var name)
            ? name
            : BuildBetDisplayName(player, displayNames);
        return $"{number}. {label}";
    }

    private static string FormatBetActionReply(
        BetActionResult result,
        IReadOnlyDictionary<string, string> displayNames)
    {
        switch (result.Status)
        {
            case BetActionStatus.Placed:
                return WithToteFooter(
                    $"""
                    Готово! Ты поставил {result.Bet!.Amount} таблов на то, что {FormatTargetLabel(result.Bet.TargetSteamId, result.PriorityTargets, displayNames)} займёт 1 место по «{BettingMarket.CategoryTitle}».
                    Остаток на счёте: {result.Balance} таблов.
                    """);

            case BetActionStatus.Replaced:
                return WithToteFooter(
                    $"""
                    Ставка обновлена: {result.Bet!.Amount} таблов на то, что {FormatTargetLabel(result.Bet.TargetSteamId, result.PriorityTargets, displayNames)} займёт 1 место по «{BettingMarket.CategoryTitle}».
                    Остаток на счёте: {result.Balance} таблов.
                    """);

            case BetActionStatus.Cancelled:
                return WithToteFooter(
                    $"""
                    Ставка отменена. {result.Bet!.Amount} таблов вернулись на счёт.
                    Сейчас на счёте: {result.Balance} таблов.
                    """);

            case BetActionStatus.NeedLink:
                return WithToteFooter(
                    $"""
                    Сначала привяжи Steam:
                        /link <ссылка на профиль Steam>

                    {BetMiniHelpText}
                    """);

            case BetActionStatus.NeedStarter:
                return WithToteFooter(
                    $"""
                    Сначала получи стартовые таблы:
                        /balance

                    {BetMiniHelpText}
                    """);

            case BetActionStatus.WindowClosed:
                return WithToteFooter(
                    $"""
                    Приём ставок уже закрыт (после {ToteEconomy.BetWindowCloseHourMsk}:00 по Москве).
                    Ждём итог игрового дня по «{BettingMarket.CategoryTitle}».

                    {BetMiniHelpText}
                    """);

            case BetActionStatus.NoPriorityTargets:
                return WithToteFooter(
                    $"""
                    Пока некого выбрать: в приоритетном списке никого нет.

                    {BetMiniHelpText}
                    """);

            case BetActionStatus.NothingToCancel:
                return WithToteFooter(
                    $"""
                    У тебя нет активной ставки, которую можно отменить.

                    {FormatTargetsAndHelp(result.PriorityTargets, result.Bet, displayNames)}
                    """);

            case BetActionStatus.InvalidTargetIndex:
                return WithToteFooter(
                    $"""
                    Такого номера в списке нет. Возьми номер из списка ниже.

                    {FormatTargetsAndHelp(result.PriorityTargets, result.Bet, displayNames)}
                    """);

            case BetActionStatus.InvalidAmount:
                return WithToteFooter(
                    $"""
                    Минимальная ставка — {ToteEconomy.MinBetTabs} таблов.

                    {FormatTargetsAndHelp(result.PriorityTargets, result.Bet, displayNames)}
                    """);

            case BetActionStatus.InsufficientBalance:
                return WithToteFooter(
                    $"""
                    Не хватает таблов (сейчас на счёте: {result.Balance}).

                    {FormatTargetsAndHelp(result.PriorityTargets, result.Bet, displayNames)}
                    """);

            default:
                return WithToteFooter(
                    $"""
                    Не удалось обработать ставку.

                    {BetMiniHelpText}
                    """);
        }
    }

    private static string FormatTargetLabel(
        string steamId,
        IReadOnlyList<Player> targets,
        IReadOnlyDictionary<string, string> displayNames)
    {
        if (displayNames.TryGetValue(steamId, out var name))
        {
            return name;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].SteamId == steamId)
            {
                return BuildBetDisplayName(targets[i], displayNames);
            }
        }

        return "этот игрок";
    }

    private static string FormatTargetsAndHelp(
        IReadOnlyList<Player> targets,
        DailyBet? currentBet,
        IReadOnlyDictionary<string, string> displayNames)
    {
        var builder = new System.Text.StringBuilder();

        if (currentBet is not null)
        {
            var targetLabel = displayNames.TryGetValue(currentBet.TargetSteamId, out var name)
                ? name
                : "выбранный игрок";
            builder.AppendLine(
                $"Твоя ставка сейчас: {currentBet.Amount} таблов на {targetLabel} (1 место по «{BettingMarket.CategoryTitle}»).");
            builder.AppendLine();
        }

        if (targets.Count == 0)
        {
            builder.AppendLine("Пока некого выбрать: в приоритетном списке никого нет.");
        }
        else
        {
            builder.AppendLine("Выбери игрока по номеру:");
            for (var i = 0; i < targets.Count; i++)
            {
                builder.AppendLine(FormatPriorityTargetLine(i + 1, targets[i], displayNames));
            }
        }

        builder.AppendLine();
        builder.Append(BetMiniHelpText);
        return builder.ToString();
    }

    private async Task TryGrantCommandRewardAndNotifyAsync(
        ITelegramBotClient bot,
        Message message,
        string command,
        string args,
        CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        var tabsAmount = CommandRewardPolicy.Resolve(command, args);
        if (tabsAmount <= 0)
        {
            return;
        }

        var result = await _playersService.TryGrantCommandRewardAsync(
            message.From.Id,
            tabsAmount,
            cancellationToken);

        if (result.Status != CommandRewardStatus.GrantedDailyComplete)
        {
            return;
        }

        var text = WithToteFooter(
            $"""
            {result.RewardsUsedToday}/{ToteEconomy.CommandRewardsPerGamingDay} наград за сегодня за использование команд получены.
            Начислено за сегодня: {result.TabsGrantedToday} таблов.
            Общий баланс: {result.Balance} таблов.
            """);

        await bot.SendMessage(
            message.Chat.Id,
            text,
            cancellationToken: cancellationToken);
    }

    private async Task HandleLinkAsync(
        ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(args))
        {
            await bot.SendMessage(
                message.Chat.Id,
                LinkUsageText,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);
            return;
        }

        var parsed = SteamProfileLinkParser.Parse(args);
        ulong? steam64 = null;

        switch (parsed)
        {
            case SteamLinkParseFailed failed:
                await bot.SendMessage(
                    message.Chat.Id,
                    failed.ErrorMessage,
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                    cancellationToken: cancellationToken);
                return;

            case SteamLinkParsedSteam64 direct:
                steam64 = direct.Steam64;
                break;

            case SteamLinkParsedVanity vanity:
                if (!_steamProfiles.HasApiKey)
                {
                    await bot.SendMessage(
                        message.Chat.Id,
                        WhoisService.MissingApiKeyMessage,
                        cancellationToken: cancellationToken);
                    return;
                }

                steam64 = await _steamProfiles.ResolveVanityAsync(vanity.Vanity, cancellationToken);

                if (steam64 is null)
                {
                    await bot.SendMessage(
                        message.Chat.Id,
                        "Не удалось найти Steam-профиль по этой ссылке /id/… Проверь адрес или используй ссылку вида /profiles/7656119…",
                        cancellationToken: cancellationToken);
                    return;
                }

                break;
        }

        if (steam64 is null
            || !SteamIdConverter.TryFromSteam64(steam64.Value, out var classicSteamId))
        {
            await bot.SendMessage(
                message.Chat.Id,
                "Не удалось разобрать Steam ID из ссылки.",
                cancellationToken: cancellationToken);
            return;
        }

        var telegramUsername = message.From.Username is { } username
            ? $"@{username}"
            : null;

        var result = await _playersService.LinkAsync(
            message.From.Id,
            telegramUsername,
            classicSteamId,
            cancellationToken);

        var reply = result.Status switch
        {
            LinkPlayerStatus.Linked =>
                $"Готово. Telegram связан со Steam.\nSteam: <code>{classicSteamId}</code>",
            LinkPlayerStatus.AlreadyLinked =>
                $"Уже связано.\nSteam: <code>{classicSteamId}</code>",
            LinkPlayerStatus.TelegramBoundToOtherSteam =>
                $"Этот Telegram уже привязан к другому Steam:\n<code>{result.Player?.SteamId}</code>",
            LinkPlayerStatus.SteamBoundToOtherTelegram =>
                "Этот Steam уже привязан к другому Telegram-аккаунту.",
            _ => "Не удалось выполнить привязку.",
        };

        await bot.SendMessage(
            message.Chat.Id,
            reply,
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
