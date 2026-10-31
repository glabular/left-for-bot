using System.Text;

namespace left_4_bot.Console;

public static class ConsolePrompts
{
    public static string ReadLine(string prompt)
    {
        System.Console.Write(prompt);

        return System.Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static long ReadLong(string prompt)
    {
        while (true)
        {
            var input = ReadLine(prompt);

            if (long.TryParse(input, out var value))
            {
                return value;
            }

            System.Console.WriteLine("Invalid number. Try again.");
        }
    }

    /// <summary>
    /// Reads a console line, or returns <see langword="null"/> if nothing was typed
    /// before <paramref name="timeout"/> elapses.
    /// Once the user starts typing, waiting continues until Enter (no timeout).
    /// Falls back to a blocking <see cref="System.Console.ReadLine"/> when input is redirected.
    /// </summary>
    public static string? ReadLineOrTimeout(TimeSpan timeout)
    {
        if (System.Console.IsInputRedirected)
        {
            return System.Console.ReadLine()?.Trim();
        }

        var deadline = DateTime.UtcNow + timeout;
        var buffer = new StringBuilder();
        var lastShownSeconds = -1;

        while (true)
        {
            while (System.Console.KeyAvailable)
            {
                var key = System.Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    ClearCountdownLine();
                    System.Console.WriteLine(buffer.ToString());
                    return buffer.ToString().Trim();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        System.Console.Write("\b \b");
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    if (lastShownSeconds >= 0)
                    {
                        ClearCountdownLine();
                        lastShownSeconds = -1;
                    }

                    buffer.Append(key.KeyChar);
                    System.Console.Write(key.KeyChar);
                }
            }

            if (buffer.Length > 0)
            {
                Thread.Sleep(50);
                continue;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                ClearCountdownLine();
                return null;
            }

            var secondsLeft = (int)Math.Ceiling(remaining.TotalSeconds);
            if (secondsLeft != lastShownSeconds)
            {
                lastShownSeconds = secondsLeft;
                System.Console.Write($"\rAuto-start in {secondsLeft}s (type a menu choice to cancel)...");
            }

            Thread.Sleep(50);
        }
    }

    private static void ClearCountdownLine()
    {
        var width = 80;
        try
        {
            if (System.Console.WindowWidth > 1)
            {
                width = System.Console.WindowWidth - 1;
            }
        }
        catch (IOException)
        {
            // Non-interactive / odd consoles: keep a fixed wipe width.
        }

        System.Console.Write("\r" + new string(' ', width) + "\r");
    }

    /// <summary>
    /// Masks a secret value before it is displayed or logged.
    /// </summary>
    /// <param name="secretString">
    /// The secret value to mask.
    /// </param>
    /// <returns>
    /// <list type="bullet">
    /// <item>
    /// <description><c>"(not set)"</c> if <paramref name="secretString"/> is <see langword="null"/>, empty, or contains only whitespace.</description>
    /// </item>
    /// <item>
    /// <description><c>"****"</c> if the token is 9 characters or fewer.</description>
    /// </item>
    /// <item>
    /// <description>The first 9 characters of the token followed by <c>"****"</c> for longer tokens.</description>
    /// </item>
    /// </list>
    /// </returns>
    public static string MaskSecret(string secretString)
    {
        if (string.IsNullOrWhiteSpace(secretString))
        {
            return "(not set)";
        }

        if (secretString.Length <= 9)
        {
            return "****";
        }

        return secretString[..9] + "****";
    }
}
