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
        var sawTransportFailure = false;

        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is ApiRequestException apiEx
                && TryGetApiErrorReason(apiEx.ErrorCode) is { } apiReason)
            {
                return apiReason;
            }

            if (ex is SocketException socket)
            {
                if (TryGetSocketErrorReason(socket.SocketErrorCode) is { } socketReason)
                {
                    return socketReason;
                }

                if (TryGetNativeErrorReason(socket.NativeErrorCode) is { } nativeReason)
                {
                    return nativeReason;
                }

                sawTransportFailure = true;
            }

            if (ex is IOException io && IsTransportIOException(io))
            {
                return "connection reset";
            }

            if (ex is HttpRequestException httpEx && IsNameResolutionFailure(httpEx))
            {
                return "host unknown";
            }

            if (ex is RequestException { Message: var requestMessage }
                && TryGetInfrastructureMessageReason(requestMessage) is { } infraReason)
            {
                return infraReason;
            }

            if (IsHttpTimeout(ex))
            {
                sawTimeout = true;
            }
        }

        if (HasTelegramServiceFailureWrapper(exception) && sawTransportFailure)
        {
            return "connection reset";
        }

        return sawTimeout ? "request timed out" : null;
    }

    private static string? TryGetApiErrorReason(int errorCode) => errorCode switch
    {
        500 => "server error",
        502 => "bad gateway",
        503 => "service unavailable",
        504 => "gateway timeout",
        _ => null
    };

    private static string? TryGetSocketErrorReason(SocketError socketError) => socketError switch
    {
        SocketError.HostNotFound => "host unknown",
        SocketError.ConnectionReset or SocketError.ConnectionAborted => "connection reset",
        SocketError.TimedOut => "request timed out",
        SocketError.NetworkUnreachable or SocketError.NetworkDown => "network unreachable",
        _ => null
    };

    private static string? TryGetNativeErrorReason(int nativeErrorCode) => nativeErrorCode switch
    {
        10054 or 10053 => "connection reset",
        11001 => "host unknown",
        10060 => "request timed out",
        10051 or 10050 or 10061 => "network unreachable",
        _ => null
    };

    private static string? TryGetInfrastructureMessageReason(string message)
    {
        if (message.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase))
        {
            return "bad gateway";
        }

        if (message.Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "gateway timeout";
        }

        if (message.Contains("Service Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return "service unavailable";
        }

        if (message.Contains("Service Failure", StringComparison.OrdinalIgnoreCase))
        {
            return "service failure";
        }

        if (message.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase))
        {
            return "server error";
        }

        return null;
    }

    private static bool IsTransportIOException(IOException exception)
    {
        var message = exception.Message;

        return message.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
            || message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("разорвал", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTelegramServiceFailureWrapper(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is RequestException { Message: var message }
                && message.Contains("Service Failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
