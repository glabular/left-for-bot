using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Persistence;

public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var player = modelBuilder.Entity<Player>();

        player.HasKey(p => p.Id);
        player.HasIndex(p => p.SteamId).IsUnique();
        player.HasIndex(p => p.TelegramUserId).IsUnique();
        player.Property(p => p.SteamId).HasMaxLength(32).IsRequired();
        player.Property(p => p.TelegramDisplayName).HasMaxLength(128);
    }
}
