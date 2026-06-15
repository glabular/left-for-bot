using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.ServerLogs;

public static class ServerLogSyncServiceCollectionExtensions
{
    public static IServiceCollection AddServerLogSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServerLogSyncOptions>(
            configuration.GetSection(ServerLogSyncOptions.SectionName));
        services.Configure<GamingDayOptions>(
            configuration.GetSection(GamingDayOptions.SectionName));
        services.Configure<PriorityPlayersOptions>(
            configuration.GetSection(PriorityPlayersOptions.SectionName));
        services.AddSingleton<IServerFtpClient, ServerFtpClient>();
        services.AddSingleton<ServerLogSyncService>();
        services.AddSingleton<FriendlyFireStatsService>();

        return services;
    }
}
