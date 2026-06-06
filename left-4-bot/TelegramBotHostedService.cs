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
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _services;
    private readonly ILogger<TelegramBotHostedService> _logger;

    private TimeSpan _currentBackoff = InitialBackoff;
    private bool _wasDisconnected;

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

        _ = RunRecoveryProbeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _bot.ReceiveAsync(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken);

                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                await HandleReceiverCrashAsync(exception, stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Telegram.Bot.Types.Update update,
        CancellationToken cancellationToken)
    {
        NotifyConnectionRestoredIfNeeded();

        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();

        await handler.HandleUpdateAsync(bot, update, cancellationToken);
    }

    private async Task HandlePollingErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error.");

        if (!TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
        {
            return;
        }

        _wasDisconnected = true;

        await Task.Delay(_currentBackoff, cancellationToken);

        _currentBackoff = TimeSpan.FromSeconds(
            Math.Min(_currentBackoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
    }

    private async Task HandleReceiverCrashAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram receiver crashed unexpectedly.");

        if (!TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
        {
            return;
        }

        _wasDisconnected = true;

        await Task.Delay(_currentBackoff, cancellationToken);

        _currentBackoff = TimeSpan.FromSeconds(
            Math.Min(_currentBackoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
    }

    private async Task RunRecoveryProbeAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!_wasDisconnected)
                {
                    continue;
                }

                try
                {
                    await _bot.GetMe(cancellationToken: stoppingToken);
                    NotifyConnectionRestoredIfNeeded();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Polling error handler logs active failures with backoff.
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void NotifyConnectionRestoredIfNeeded()
    {
        if (!_wasDisconnected)
        {
            return;
        }

        _logger.LogInformation("Telegram connection restored.");
        _wasDisconnected = false;
        _currentBackoff = InitialBackoff;
    }
}
