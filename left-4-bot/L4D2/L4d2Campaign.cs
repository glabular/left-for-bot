namespace left_4_bot.L4D2;

public readonly record struct L4d2Campaign(string EnglishName, string RussianName)
{
    public string DisplayName => $"{EnglishName} ({RussianName})";
}
