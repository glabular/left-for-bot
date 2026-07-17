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
