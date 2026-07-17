using Telegram.Bot;

namespace left_4_bot;

public sealed class BotUsernameProvider
{
    private readonly ITelegramBotClient _bot;
    private string? _username;

    public BotUsernameProvider(ITelegramBotClient bot) => _bot = bot;

    public async Task<string> GetBotUsernameAsync(CancellationToken cancellationToken)
    {
        if (_username is not null)
        {
            return _username;
        }

        var me = await _bot.GetMe(cancellationToken);
        _username = me.Username ?? throw new InvalidOperationException("Bot has no username.");

        return _username;
    }
}
