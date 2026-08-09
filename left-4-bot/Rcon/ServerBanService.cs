using System.Globalization;

using left_4_bot.Whois;

namespace left_4_bot.Rcon;

/// <summary>
/// Bans and unbans Steam IDs on the game server via RCON.
/// </summary>
public sealed class ServerBanService
{
    private readonly ServerRconService _rcon;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public ServerBanService(ServerRconService rcon)
    {
        _rcon = rcon;
    }

    /// <summary>
    /// Loads the current Steam ID ban list from the server.
    /// </summary>
    public async Task<(bool IsSuccess, string Message, IReadOnlyList<ServerBanListEntry> Entries)> GetBanListAsync(
        CancellationToken cancellationToken)
    {
        var configError = _rcon.GetConfigurationError();
        if (configError is not null)
        {
            return (false, configError, []);
        }

        var result = await _rcon.TryExecuteAsync("listid", cancellationToken);
        if (!result.IsSuccess)
        {
            return (false, result.Message, []);
        }

        var entries = BanIdListParser.Parse(result.Message);
        return (true, result.Message, entries);
    }

    /// <summary>
    /// Permanently bans a Steam ID and persists the ban list.
    /// </summary>
    public async Task<ServerBanActionResult> BanAsync(
        string classicSteamId,
        CancellationToken cancellationToken)
    {
        var configError = _rcon.GetConfigurationError();
        if (configError is not null)
        {
            return new ServerBanActionResult(false, configError);
        }

        if (string.IsNullOrWhiteSpace(classicSteamId))
        {
            return new ServerBanActionResult(false, "Некорректный Steam ID.");
        }

        if (!await _mutex.WaitAsync(0, cancellationToken))
        {
            return new ServerBanActionResult(
                false,
                "Уже выполняется другая команда бана/разбана. Подождите.");
        }

        try
        {
            var banCommand = string.Create(
                CultureInfo.InvariantCulture,
                $"banid 0 {classicSteamId.Trim()} kick");

            var ban = await _rcon.TryExecuteAsync(banCommand, cancellationToken);
            if (!ban.IsSuccess)
            {
                return new ServerBanActionResult(false, ban.Message);
            }

            if (LooksAlreadyBanned(ban.Message))
            {
                return new ServerBanActionResult(
                    false,
                    "Этот игрок уже в бане на сервере. Снимите бан через /unban.");
            }

            var write = await _rcon.TryExecuteAsync("writeid", cancellationToken);
            if (!write.IsSuccess)
            {
                return new ServerBanActionResult(
                    false,
                    $"Бан отправлен, но не удалось сохранить список (writeid): {write.Message}\n"
                    + "Бан может исчезнуть после перезапуска сервера.");
            }

            // Success text (with player name / HTML) is built by the command handler.
            var detail = string.IsNullOrWhiteSpace(ban.Message)
                ? string.Empty
                : ban.Message.Trim();

            return new ServerBanActionResult(true, detail);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Removes a ban by engine ban-list slot and persists the ban list.
    /// </summary>
    public async Task<ServerBanActionResult> UnbanAsync(
        int banSlot,
        string classicSteamId,
        CancellationToken cancellationToken)
    {
        var configError = _rcon.GetConfigurationError();
        if (configError is not null)
        {
            return new ServerBanActionResult(false, configError);
        }

        if (!await _mutex.WaitAsync(0, cancellationToken))
        {
            return new ServerBanActionResult(
                false,
                "Уже выполняется другая команда бана/разбана. Подождите.");
        }

        try
        {
            var removeCommand = string.Create(
                CultureInfo.InvariantCulture,
                $"removeid {banSlot}");

            var remove = await _rcon.TryExecuteAsync(removeCommand, cancellationToken);
            if (!remove.IsSuccess)
            {
                return new ServerBanActionResult(false, remove.Message);
            }

            if (LooksRemoveFailed(remove.Message))
            {
                return new ServerBanActionResult(
                    false,
                    string.IsNullOrWhiteSpace(remove.Message)
                        ? "Не удалось снять бан (removeid)."
                        : remove.Message.Trim());
            }

            var write = await _rcon.TryExecuteAsync("writeid", cancellationToken);
            if (!write.IsSuccess)
            {
                return new ServerBanActionResult(
                    false,
                    $"Бан снят в памяти сервера, но не удалось сохранить список (writeid): {write.Message}\n"
                    + "После перезапуска бан может вернуться.");
            }

            // Success text is built by the command handler.
            return new ServerBanActionResult(true, string.Empty);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>
    /// Finds a ban by classic Steam ID via <c>listid</c>, then removes and persists it.
    /// </summary>
    public async Task<ServerBanActionResult> UnbanBySteamIdAsync(
        string classicSteamId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(classicSteamId))
        {
            return new ServerBanActionResult(false, "Некорректный Steam ID.");
        }

        if (!SteamIdConverter.TryToCanonicalClassic(classicSteamId.Trim(), out var canonical))
        {
            canonical = classicSteamId.Trim();
        }

        var (isSuccess, message, entries) = await GetBanListAsync(cancellationToken);
        if (!isSuccess)
        {
            return new ServerBanActionResult(false, message);
        }

        var entry = entries.FirstOrDefault(e =>
            string.Equals(e.ClassicSteamId, canonical, StringComparison.Ordinal));

        if (entry is null)
        {
            return new ServerBanActionResult(
                false,
                "Этот игрок не в бане на сервере.");
        }

        return await UnbanAsync(entry.BanSlot, entry.ClassicSteamId, cancellationToken);
    }

    private static bool LooksAlreadyBanned(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        return response.Contains("already", StringComparison.OrdinalIgnoreCase)
            || response.Contains("уже", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksRemoveFailed(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        return response.Contains("couldn't find", StringComparison.OrdinalIgnoreCase)
            || response.Contains("could not find", StringComparison.OrdinalIgnoreCase)
            || response.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || response.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }
}
