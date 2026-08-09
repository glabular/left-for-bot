using left_4_bot.Rcon;
using left_4_bot.ServerLogs;

namespace left_4_bot.Tests.Rcon;

public sealed class PlayerModerationSelectionStoreTests
{
    [Fact]
    public void TryGetBanCandidate_RequiresPriorList_AndOneBasedIndex()
    {
        var store = new PlayerModerationSelectionStore();

        Assert.False(store.TryGetBanCandidate(1, out _, out var missingListError));
        Assert.Contains("/ban", missingListError, StringComparison.Ordinal);

        store.SetBanCandidates(
        [
            new RecentPlayerEntry("A", "STEAM_1:0:1"),
            new RecentPlayerEntry("B", "STEAM_1:0:2"),
        ]);

        Assert.False(store.TryGetBanCandidate(0, out _, out var zeroError));
        Assert.Contains("от 1", zeroError, StringComparison.OrdinalIgnoreCase);

        Assert.False(store.TryGetBanCandidate(3, out _, out var rangeError));
        Assert.Contains("Номера 3 нет в списке", rangeError, StringComparison.Ordinal);
        Assert.Contains("посмотреть список", rangeError, StringComparison.Ordinal);

        Assert.True(store.TryGetBanCandidate(2, out var entry, out _));
        Assert.Equal("B", entry!.Name);
        Assert.Equal("STEAM_1:0:2", entry.ClassicSteamId);
    }

    [Fact]
    public void TryGetUnbanEntry_UsesIndependentList()
    {
        var store = new PlayerModerationSelectionStore();
        store.SetBanCandidates([new RecentPlayerEntry("A", "STEAM_1:0:1")]);

        Assert.False(store.TryGetUnbanEntry(1, out _, out var error));
        Assert.Contains("/unban", error, StringComparison.Ordinal);

        store.SetUnbanEntries(
        [
            new UnbanSelectionEntry(0, "STEAM_1:0:9", "Nick", "permanent"),
        ]);

        Assert.True(store.TryGetUnbanEntry(1, out var entry, out _));
        Assert.Equal(0, entry!.BanSlot);
        Assert.Equal("STEAM_1:0:9", entry.ClassicSteamId);
        Assert.Equal("Nick", entry.DisplayName);
    }

    [Fact]
    public void TryGetBanCandidate_ExpiresAfterTtl()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero));
        var store = new PlayerModerationSelectionStore(time);

        store.SetBanCandidates([new RecentPlayerEntry("A", "STEAM_1:0:1")]);

        time.UtcNowValue = time.UtcNowValue.Add(PlayerModerationSelectionStore.SelectionTtl);
        Assert.True(store.TryGetBanCandidate(1, out var stillValid, out _));
        Assert.Equal("A", stillValid!.Name);

        time.UtcNowValue = time.UtcNowValue.AddSeconds(1);
        Assert.False(store.TryGetBanCandidate(1, out _, out var expiredError));
        Assert.Contains("/ban", expiredError, StringComparison.Ordinal);
        Assert.Contains("без параметров", expiredError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetUnbanEntry_ExpiresAfterTtl()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero));
        var store = new PlayerModerationSelectionStore(time);

        store.SetUnbanEntries(
        [
            new UnbanSelectionEntry(0, "STEAM_1:0:9", "Nick", "permanent"),
        ]);

        time.UtcNowValue = time.UtcNowValue.AddMinutes(11);
        Assert.False(store.TryGetUnbanEntry(1, out _, out var expiredError));
        Assert.Contains("/unban", expiredError, StringComparison.Ordinal);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public ManualTimeProvider(DateTimeOffset utcNow) => UtcNowValue = utcNow;

        public DateTimeOffset UtcNowValue { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
