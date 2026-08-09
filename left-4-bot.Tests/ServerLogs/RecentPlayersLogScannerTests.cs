using left_4_bot.ServerLogs;

namespace left_4_bot.Tests.ServerLogs;

public sealed class RecentPlayersLogScannerTests
{
    [Fact]
    public void ScanText_FiltersByWindow_DedupesCanonicalSteam_KeepsLatestName_SortsRecentFirst()
    {
        // Window: 2026-08-08 10:00 → 2026-08-08 12:00 Moscow (Kind-unspecified, as in logs).
        var bounds = new GamingDayBounds(
            new DateTime(2026, 8, 8, 10, 0, 0),
            new DateTime(2026, 8, 8, 12, 0, 0));

        const string log =
            """
            L 08/08/2026 - 09:59:59: "OldNick<2><STEAM_0:1:100><>" connected
            L 08/08/2026 - 10:05:00: "Alice<3><STEAM_0:1:100><>" entered the game
            L 08/08/2026 - 10:30:00: "Bob<4><STEAM_1:0:200><>" connected
            L 08/08/2026 - 11:00:00: "AliceNew<5><STEAM_1:1:100><>" entered the game
            L 08/08/2026 - 12:00:00: "TooLate<6><STEAM_1:0:300><>" connected
            """;

        var players = RecentPlayersLogScanner.ScanText(log, bounds);

        Assert.Equal(2, players.Count);
        Assert.Equal("STEAM_1:1:100", players[0].ClassicSteamId);
        Assert.Equal("AliceNew", players[0].Name);
        Assert.Equal("STEAM_1:0:200", players[1].ClassicSteamId);
        Assert.Equal("Bob", players[1].Name);
    }

    [Fact]
    public void ScanText_Empty_ReturnsEmpty()
    {
        var bounds = new GamingDayBounds(
            new DateTime(2026, 8, 8, 0, 0, 0),
            new DateTime(2026, 8, 9, 0, 0, 0));

        Assert.Empty(RecentPlayersLogScanner.ScanText("", bounds));
    }
}
