using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace left_4_bot.Handlers;

public sealed class UpdateHandler : IUpdateHandler
{
    private readonly BotSettings _settings;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(BotSettings settings, ILogger<UpdateHandler> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return Task.CompletedTask;
        }

        if (_settings.GroupChatId == 0)
        {
            _logger.LogInformation(
                "Chat Id: {ChatId}, Type: {ChatType}, Text: {Text}",
                message.Chat.Id,
                message.Chat.Type,
                message.Text);

            return Task.CompletedTask;
        }

        if (message.Chat.Id != _settings.GroupChatId)
        {
            _logger.LogDebug(
                "Ignored message from chat {ChatId}.",
                message.Chat.Id);

            return Task.CompletedTask;
        }

        var user = message.From?.Username ?? message.From?.FirstName ?? "unknown";
        _logger.LogInformation("Group message from {User}: {Text}", user, message.Text);

        return HandleGroupMessageAsync(message, cancellationToken);
    }

    public Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Update handler error from {Source}.", source);

        return Task.CompletedTask;
    }

    private Task HandleGroupMessageAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
