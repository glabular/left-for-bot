using left_4_bot;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public sealed class UpdateHandler : IUpdateHandler
{
    private readonly BotSettings _settings;
    private readonly BotUsernameProvider _usernameProvider;
    private readonly GroupCommandHandler _commandHandler;
    private readonly TelegramConnectionMonitor _connectionMonitor;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        BotSettings settings,
        BotUsernameProvider usernameProvider,
        GroupCommandHandler commandHandler,
        TelegramConnectionMonitor connectionMonitor,
        ILogger<UpdateHandler> logger)
    {
        _settings = settings;
        _usernameProvider = usernameProvider;
        _commandHandler = commandHandler;
        _connectionMonitor = connectionMonitor;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        if (_settings.GroupChatId == 0)
        {
            _logger.LogInformation(
                "Chat Id: {ChatId}, Type: {ChatType}, Text: {Text}",
                message.Chat.Id,
                message.Chat.Type,
                message.Text);

            return;
        }

        if (message.Chat.Id != _settings.GroupChatId)
        {
            _logger.LogDebug(
                "Ignored message from chat {ChatId}.",
                message.Chat.Id);

            return;
        }

        var user = message.From?.Username ?? message.From?.FirstName ?? "unknown";
        _logger.LogInformation("Group message from {User}: {Text}", user, message.Text);

        await HandleGroupMessageAsync(bot, message, cancellationToken);
    }

    public Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        if (TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
        {
            _connectionMonitor.ReportTransientFailure(exception, $"handler:{source}");
            return Task.CompletedTask;
        }

        _connectionMonitor.ReportFatalFailure(exception, $"handler:{source}");

        return Task.CompletedTask;
    }

    private async Task HandleGroupMessageAsync(
        ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        var botUsername = await _usernameProvider.GetBotUsernameAsync(cancellationToken);

        if (!CommandParser.TryGetCommand(message, botUsername, out var command, out var args))
        {
            return;
        }

        await _commandHandler.HandleAsync(bot, message, command, args, cancellationToken);
    }
}
