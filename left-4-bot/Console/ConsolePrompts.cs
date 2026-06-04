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

    public static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "(not set)";
        }

        if (token.Length <= 9)
        {
            return "****";
        }

        return token[..9] + "****";
    }
}
