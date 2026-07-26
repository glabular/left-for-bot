using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Persistence;

public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();

    public DbSet<DailyMarket> DailyMarkets => Set<DailyMarket>();

    public DbSet<DailyBet> DailyBets => Set<DailyBet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var player = modelBuilder.Entity<Player>();

        player.HasKey(p => p.Id);
        player.HasIndex(p => p.SteamId).IsUnique();
        player.HasIndex(p => p.TelegramUserId).IsUnique();
        player.Property(p => p.SteamId).HasMaxLength(32).IsRequired();
        player.Property(p => p.TelegramUsername).HasMaxLength(128);
        player.Property(p => p.Balance).HasDefaultValue(0);

        var market = modelBuilder.Entity<DailyMarket>();

        market.HasKey(m => m.Id);
        market.HasIndex(m => m.GamingDayAnchor).IsUnique();
        market.Property(m => m.Status).HasConversion<int>();

        var bet = modelBuilder.Entity<DailyBet>();

        bet.HasKey(b => b.Id);
        bet.HasIndex(b => new { b.GamingDayAnchor, b.TelegramUserId }).IsUnique();
        bet.Property(b => b.TargetSteamId).HasMaxLength(32).IsRequired();
    }
}
