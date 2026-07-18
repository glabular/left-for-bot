using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace left_4_bot.TelegramClient;

/// <summary>
/// Wraps <see cref="ITelegramBotClient"/> to throttle chat sends and retry on HTTP 429.
/// </summary>
public sealed class ThrottlingTelegramBotClient : ITelegramBotClient
{
    private const int MaxFloodRetryAttempts = 5;

    private readonly ITelegramBotClient _inner;
    private readonly TelegramSendRateLimiter _limiter;
    private readonly ILogger<ThrottlingTelegramBotClient> _logger;

    public ThrottlingTelegramBotClient(
        ITelegramBotClient inner,
        ILogger<ThrottlingTelegramBotClient> logger)
    {
        _inner = inner;
        _limiter = new TelegramSendRateLimiter();
        _logger = logger;
    }

    public bool LocalBotServer => _inner.LocalBotServer;

    public long BotId => _inner.BotId;

    public TimeSpan Timeout
    {
        get => _inner.Timeout;
        set => _inner.Timeout = value;
    }

    public IExceptionParser ExceptionsParser
    {
        get => _inner.ExceptionsParser;
        set => _inner.ExceptionsParser = value;
    }

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
    {
        add => _inner.OnMakingApiRequest += value;
        remove => _inner.OnMakingApiRequest -= value;
    }

    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
    {
        add => _inner.OnApiResponseReceived += value;
        remove => _inner.OnApiResponseReceived -= value;
    }

    public async Task<TResponse> SendRequest<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        // Only throttle chat-targeted calls. GetUpdates / GetMe must stay unqueued.
        if (request is IChatTargetable chatTarget)
        {
            await _limiter.WaitAsync(chatTarget.ChatId, cancellationToken);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _inner.SendRequest(request, cancellationToken);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 429 && attempt < MaxFloodRetryAttempts)
            {
                // Telegram tells us how long to wait before repeating this request.
                var waitSeconds = ex.Parameters?.RetryAfter ?? 1;
                _logger.LogWarning(
                    "Telegram rate limit (429); waiting {WaitSeconds}s then retrying ({Attempt}/{MaxAttempts}).",
                    waitSeconds,
                    attempt,
                    MaxFloodRetryAttempts);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
            }
        }
    }

    public Task<bool> TestApi(CancellationToken cancellationToken = default)
    {
        return _inner.TestApi(cancellationToken);
    }

    public Task DownloadFile(
        string filePath,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        _inner.DownloadFile(filePath, destination, cancellationToken);

    public Task DownloadFile(
        TGFile file,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        _inner.DownloadFile(file, destination, cancellationToken);
}
