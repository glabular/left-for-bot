namespace left_4_bot.ServerLogs;

public static class PlayStatsCategories
{
    public static IReadOnlyList<PlayStatsCategory> All { get; } =
    [
        new("Убито обычных заражённых", player => player.Common),
        new("Убито особых заражённых", player => player.SiKilled),
        new("Выстрелов в голову", player => player.Headshots),
        new("Урон по ведьме", player => player.WitchDamage),
    ];
}
