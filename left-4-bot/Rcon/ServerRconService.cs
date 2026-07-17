using System.Net;
using CoreRCON;
using Microsoft.Extensions.Options;

namespace left_4_bot.Rcon;

public sealed class ServerRconService
{
    private const int TelegramMessageLimit = 4096;
    private const string TruncationSuffix = "\n\n… (обрезано)";

    public const string SetupHint =
        "RCON не настроен. Выполните:\n" +
        "  dotnet user-secrets set \"Rcon:Host\" \"46.174.48.12\"\n" +
        "  dotnet user-secrets set \"Rcon:Port\" \"27217\"\n" +
        "  dotnet user-secrets set \"Rcon:Password\" \"YOUR_PASSWORD\"";

    private readonly RconOptions _options;

    public ServerRconService(IOptions<RconOptions> options)
    {
        _options = options.Value;
    }

    public string? GetConfigurationError() => ValidateOptions();

    public async Task<ServerRconResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await TryExecuteAsync("status", cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        return new ServerRconResult(true, TruncateForTelegram(result.Message));
    }

    public Task<ServerRconResult> TryExecuteAsync(
        string command,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(command, cancellationToken);

    public async Task<bool> IsRconReachableAsync(CancellationToken cancellationToken)
    {
        var result = await TryExecuteAsync("status", cancellationToken);
        return result.IsSuccess;
    }

    private async Task<ServerRconResult> ExecuteAsync(
        string command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configError = ValidateOptions();
        if (configError is not null)
        {
            return new ServerRconResult(false, configError);
        }

        RCON? rcon = null;

        try
        {
            rcon = CreateClient();
            await rcon.ConnectAsync();

            var response = await rcon.SendCommandAsync(command);
            return new ServerRconResult(true, response);
        }
        catch (Exception ex)
        {
            return new ServerRconResult(false, $"Ошибка RCON: {ex.Message}");
        }
        finally
        {
            SafeDispose(rcon);
        }
    }

    private static void SafeDispose(RCON? rcon)
    {
        if (rcon is null)
        {
            return;
        }

        try
        {
            rcon.Dispose();
        }
        catch (Exception)
        {
            // Failed connections may throw during socket shutdown; cleanup errors must not mask RCON results.
        }
    }

    private RCON CreateClient() =>
        new(
            IPAddress.Parse(_options.Host.Trim()),
            (ushort)_options.Port,
            _options.Password);

    private string? ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            return SetupHint;
        }

        return null;
    }

    private static string TruncateForTelegram(string text)
    {
        if (text.Length <= TelegramMessageLimit)
        {
            return text;
        }

        var maxLength = TelegramMessageLimit - TruncationSuffix.Length;
        return text[..maxLength] + TruncationSuffix;
    }
}
