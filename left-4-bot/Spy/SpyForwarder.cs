using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace left_4_bot.Spy;

/// <summary>
/// Queues spy chat lines and sends them to the Telegram group behind the bot client's rate limiter.
/// </summary>
public sealed class SpyForwarder : BackgroundService
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ITelegramBotClient _bot;
    private readonly BotSettings _botSettings;
    private readonly ILogger<SpyForwarder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpyForwarder"/> class.
    /// </summary>
    /// <param name="bot">Telegram bot client (expected to be rate-limited).</param>
    /// <param name="botSettings">Bot settings, including the group chat ID.</param>
    /// <param name="logger">Logger for this forwarder.</param>
    public SpyForwarder(
        ITelegramBotClient bot,
        BotSettings botSettings,
        ILogger<SpyForwarder> logger)
    {
        _bot = bot;
        _botSettings = botSettings;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a formatted chat line for delivery to the group.
    /// </summary>
    /// <param name="text">Plain-text message to send.</param>
    public void Enqueue(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!_channel.Writer.TryWrite(text))
        {
            _logger.LogWarning("Spy forwarder queue rejected a message");
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var text in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            if (_botSettings.GroupChatId == 0)
            {
                continue;
            }

            try
            {
                await _bot.SendMessage(
                    _botSettings.GroupChatId,
                    text,
                    disableNotification: true,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send spy chat line to Telegram");
            }
        }
    }
}
