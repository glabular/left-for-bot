using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace left_4_bot.Persistence;

public sealed class BotDbContextFactory : IDesignTimeDbContextFactory<BotDbContext>
{
    public BotDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "bot.db");
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new BotDbContext(options);
    }
}
