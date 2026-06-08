using System.Net.Sockets;
using Telegram.Bot.Exceptions;

namespace left_4_bot;

internal static class TelegramNetworkErrorClassifier
{
    public static bool IsTransientNetworkError(Exception exception) =>
        GetShortReason(exception) is not null;

    public static string? GetShortReason(Exception exception)
    {
        var sawTimeout = false;

        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SocketException { SocketErrorCode: var socketError }
                && TryGetSocketErrorReason(socketError) is { } socketReason)
            {
                return socketReason;
            }

            if (ex is HttpRequestException httpEx && IsNameResolutionFailure(httpEx))
            {
                return "host unknown";
            }

            if (IsHttpTimeout(ex))
            {
                sawTimeout = true;
            }
        }

        return sawTimeout ? "request timed out" : null;
    }

    private static string? TryGetSocketErrorReason(SocketError socketError) => socketError switch
    {
        SocketError.HostNotFound => "host unknown",
        SocketError.ConnectionReset or SocketError.ConnectionAborted => "connection reset",
        SocketError.TimedOut => "request timed out",
        SocketError.NetworkUnreachable or SocketError.NetworkDown => "network unreachable",
        _ => null
    };

    private static bool IsHttpTimeout(Exception exception) => exception switch
    {
        TimeoutException => true,
        TaskCanceledException { Message: var message } => HasTimeoutWording(message),
        RequestException { Message: var message } => HasTimeoutWording(message),
        _ => false
    };

    private static bool HasTimeoutWording(string message) =>
        message.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
        || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
        || message.Contains("elapsing", StringComparison.OrdinalIgnoreCase);

    private static bool IsNameResolutionFailure(HttpRequestException exception)
    {
        var message = exception.Message;

        return message.Contains("host is unknown", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Этот хост неизвестен", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
            || message.Contains("name or service not known", StringComparison.OrdinalIgnoreCase);
    }
}
