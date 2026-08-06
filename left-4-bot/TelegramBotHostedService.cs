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
    private readonly TelegramConnectionMonitor _connectionMonitor;
    private readonly ILogger<TelegramBotHostedService> _logger;

    private TimeSpan _currentBackoff = InitialBackoff;

    public TelegramBotHostedService(
        ITelegramBotClient bot,
        IServiceProvider services,
        TelegramConnectionMonitor connectionMonitor,
        ILogger<TelegramBotHostedService> logger)
    {
        _bot = bot;
        _services = services;
        _connectionMonitor = connectionMonitor;
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

    private Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Telegram.Bot.Types.Update update,
        CancellationToken cancellationToken)
    {
        ResetBackoffIfRecovered();

        // ReceiveAsync awaits this callback before handing the next update. Returning
        // completed lets updates overlap so HeavyCommandGate can reject a second /stat
        // while the first is still running (otherwise the gate is always free).
        _ = ProcessUpdateAsync(bot, update, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ProcessUpdateAsync(
        ITelegramBotClient bot,
        Telegram.Bot.Types.Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
            await handler.HandleUpdateAsync(bot, update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
            {
                _connectionMonitor.ReportTransientFailure(exception, "handler");
                return;
            }

            _logger.LogError(exception, "Unhandled error while processing update {UpdateId}.", update.Id);
            _connectionMonitor.ReportFatalFailure(exception, "handler");
        }
    }

    private async Task HandlePollingErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
        {
            await HandleTransientNetworkErrorAsync(exception, cancellationToken);
            return;
        }

        _connectionMonitor.ReportFatalFailure(exception, "polling");
    }

    private async Task HandleReceiverCrashAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (TelegramNetworkErrorClassifier.IsTransientNetworkError(exception))
        {
            await HandleTransientNetworkErrorAsync(exception, cancellationToken);
            return;
        }

        _connectionMonitor.ReportFatalFailure(exception, "polling");
    }

    private async Task HandleTransientNetworkErrorAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        _connectionMonitor.ReportTransientFailure(exception, "polling");
        await BackoffAfterTransientErrorAsync(cancellationToken);
    }

    private async Task BackoffAfterTransientErrorAsync(CancellationToken cancellationToken)
    {
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
                if (!_connectionMonitor.IsDisconnected)
                {
                    continue;
                }

                try
                {
                    await _bot.GetMe(cancellationToken: stoppingToken);
                    ResetBackoffIfRecovered();
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

    private void ResetBackoffIfRecovered()
    {
        if (!_connectionMonitor.TryMarkConnectionRestored())
        {
            return;
        }

        _currentBackoff = InitialBackoff;
    }
}
