using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddBotPersistence(this IServiceCollection services, string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContextFactory<BotDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<PlayersService>();
        // Market rollover and early-close stay in the repo but are not started:
        // they would poll SQLite every minute and force FTP log tailing until 20:00 MSK.
        services.AddSingleton<BettingService>();

        return services;
    }
}
