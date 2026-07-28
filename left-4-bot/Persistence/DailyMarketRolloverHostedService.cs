using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace left_4_bot.Persistence;

/// <summary>
/// Periodically runs <see cref="DailyMarketRolloverService.TickAsync"/> to settle past markets.
/// </summary>
public sealed class DailyMarketRolloverHostedService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

    private readonly DailyMarketRolloverService _rollover;
    private readonly ILogger<DailyMarketRolloverHostedService> _logger;

    public DailyMarketRolloverHostedService(
        DailyMarketRolloverService rollover,
        ILogger<DailyMarketRolloverHostedService> logger)
    {
        _rollover = rollover;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        _logger.LogInformation(
            "Daily market rollover hosted service started (interval {IntervalSeconds}s)",
            TickInterval.TotalSeconds);

        // Run once immediately so catch-up does not wait a full minute after startup.
        await RunTickSafelyAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunTickSafelyAsync(stoppingToken);
        }
    }

    private async Task RunTickSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _rollover.TickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Daily market rollover tick failed");
        }
    }
}
