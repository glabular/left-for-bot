using System.Net.Sockets;

namespace left_4_bot;

internal static class TelegramNetworkErrorClassifier
{
    public static bool IsTransientNetworkError(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SocketException { SocketErrorCode: SocketError.HostNotFound })
            {
                return true;
            }

            if (ex is HttpRequestException httpEx && IsNameResolutionFailure(httpEx))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNameResolutionFailure(HttpRequestException exception)
    {
        var message = exception.Message;

        return message.Contains("host is unknown", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Этот хост неизвестен", StringComparison.OrdinalIgnoreCase)
            || message.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
            || message.Contains("name or service not known", StringComparison.OrdinalIgnoreCase);
    }
}
