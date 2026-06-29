using System.Net;
using CoreRCON;
using Microsoft.Extensions.Options;

namespace left_4_bot.Rcon;

public sealed class ServerRconService
{
    private const int TelegramMessageLimit = 4096;
    private const string TruncationSuffix = "\n\n… (обрезано)";

    private const string SetupHint =
        "RCON не настроен. Выполните:\n" +
        "  dotnet user-secrets set \"Rcon:Host\" \"46.174.48.12\"\n" +
        "  dotnet user-secrets set \"Rcon:Port\" \"27217\"\n" +
        "  dotnet user-secrets set \"Rcon:Password\" \"YOUR_PASSWORD\"";

    private readonly RconOptions _options;

    public ServerRconService(IOptions<RconOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ServerRconResult> GetStatusAsync(CancellationToken cancellationToken)
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
            rcon = new RCON(
                IPAddress.Parse(_options.Host.Trim()),
                (ushort)_options.Port,
                _options.Password);

            await rcon.ConnectAsync();

            var response = await rcon.SendCommandAsync("status");
            return new ServerRconResult(true, TruncateForTelegram(response));
        }
        catch (Exception ex)
        {
            return new ServerRconResult(false, $"Ошибка RCON: {ex.Message}");
        }
        finally
        {
            rcon?.Dispose();
        }
    }

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
