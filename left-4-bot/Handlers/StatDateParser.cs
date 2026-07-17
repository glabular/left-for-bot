using System.Globalization;

namespace left_4_bot.Handlers;

/// <summary>
/// Parses flexible Russian-style date arguments for <c>/stat</c>
/// (<c>d</c>, <c>d.M</c>, <c>d.M.yy</c>, <c>d.M.yyyy</c>).
/// </summary>
public static class StatDateParser
{
    public static bool TryParse(string input, DateOnly today, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Trim().Split('.');

        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        if (!TryParsePart(parts[0], out var day))
        {
            return false;
        }

        int month;
        int year;

        switch (parts.Length)
        {
            case 1:
                month = today.Month;
                year = today.Year;
                break;
            case 2:
                if (!TryParsePart(parts[1], out month))
                {
                    return false;
                }

                year = today.Year;
                break;
            default:
                if (!TryParsePart(parts[1], out month) ||
                    !TryParseYear(parts[2], out year))
                {
                    return false;
                }

                break;
        }

        if (month is < 1 or > 12 || day < 1)
        {
            return false;
        }

        if (year is < 1 or > 9999)
        {
            return false;
        }

        var daysInMonth = DateTime.DaysInMonth(year, month);

        if (day > daysInMonth)
        {
            return false;
        }

        date = new DateOnly(year, month, day);

        return true;
    }

    private static bool TryParsePart(string part, out int value)
    {
        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseYear(string part, out int year)
    {
        year = 0;

        if (!TryParsePart(part, out var parsed))
        {
            return false;
        }

        // "25" → 2025; "2026" → 2026; three digits ("025") are rejected.
        if (part.Length is 1 or 2 && parsed is >= 0 and <= 99)
        {
            year = 2000 + parsed;

            return true;
        }

        if (part.Length == 4 && parsed is >= 1 and <= 9999)
        {
            year = parsed;

            return true;
        }

        return false;
    }
}
