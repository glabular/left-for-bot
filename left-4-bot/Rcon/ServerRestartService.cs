using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.Rcon;

public sealed class ServerRestartService
{
    private readonly ServerRconService _rcon;
    private readonly RconOptions _options;
    private readonly ILogger<ServerRestartService> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public ServerRestartService(
        ServerRconService rcon,
        IOptions<RconOptions> options,
        ILogger<ServerRestartService> logger)
    {
        _rcon = rcon;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServerRestartOutcome?> TryBeginServerRestartAsync(
        long? userId,
        string? username,
        CancellationToken cancellationToken)
    {
        if (!await _mutex.WaitAsync(0, cancellationToken))
        {
            return new ServerRestartOutcome(
                ServerRestartState.Rejected,
                "Перезагрузка уже выполняется.");
        }

        try
        {
            _logger.LogInformation(
                "Server restart initiated by user {UserId} ({Username})",
                userId,
                username ?? "unknown");

            if (!await _rcon.IsRconReachableAsync(cancellationToken))
            {
                ReleaseMutex();
                return new ServerRestartOutcome(
                    ServerRestartState.QuitFailed,
                    "Сервер не отвечает по RCON, перезагрузка невозможна.");
            }

            var quit = await _rcon.TryExecuteAsync("quit", cancellationToken);
            if (!quit.IsSuccess)
            {
                ReleaseMutex();
                return new ServerRestartOutcome(
                    ServerRestartState.QuitFailed,
                    "Сервер не отвечает по RCON, перезагрузка невозможна.");
            }

            _logger.LogInformation(
                "RCON quit sent by user {UserId} ({Username})",
                userId,
                username ?? "unknown");

            return null;
        }
        catch
        {
            ReleaseMutex();
            throw;
        }
    }

    public async Task<ServerRestartOutcome> WaitForServerOnlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pollInterval = TimeSpan.FromSeconds(_options.RestartPollIntervalSeconds);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(_options.RestartWaitTimeoutSeconds);

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(pollInterval, cancellationToken);

                if (await _rcon.IsRconReachableAsync(cancellationToken))
                {
                    return new ServerRestartOutcome(
                        ServerRestartState.ServerOnline,
                        "Сервер снова online.");
                }
            }

            return new ServerRestartOutcome(
                ServerRestartState.WaitTimedOut,
                "Команда отправлена, но сервер не ответил за 2 мин. Проверьте панель хостинга.");
        }
        finally
        {
            ReleaseMutex();
        }
    }

    private void ReleaseMutex()
    {
        if (_mutex.CurrentCount == 0)
        {
            _mutex.Release();
        }
    }
}
