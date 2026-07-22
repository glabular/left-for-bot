using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Persistence;

public sealed class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options)
    {
    }
}
