using left_4_bot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace left_4_bot;

public sealed class TelegramBotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _services;
    private readonly ILogger<TelegramBotHostedService> _logger;

    public TelegramBotHostedService(
        ITelegramBotClient bot,
        IServiceProvider services,
        ILogger<TelegramBotHostedService> logger)
    {
        _bot = bot;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Telegram bot polling...");

        await _bot.DeleteWebhook(cancellationToken: stoppingToken, dropPendingUpdates: true);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Telegram.Bot.Types.Update update,
        CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

        await handler.HandleUpdateAsync(bot, update, cancellationToken);
    }

    private Task HandlePollingErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error.");

        return Task.CompletedTask;
    }
}