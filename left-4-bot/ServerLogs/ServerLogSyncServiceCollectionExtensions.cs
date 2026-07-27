using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace left_4_bot.ServerLogs;

public static class ServerLogSyncServiceCollectionExtensions
{
    public static IServiceCollection AddServerLogSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddLogging();
        services.Configure<ServerLogSyncOptions>(
            configuration.GetSection(ServerLogSyncOptions.SectionName));
        services.Configure<GamingDayOptions>(
            configuration.GetSection(GamingDayOptions.SectionName));        
        services.Configure<ServerLogTailOptions>(
            configuration.GetSection(ServerLogTailOptions.SectionName));
        services.AddSingleton<IServerFtpClient, ServerFtpClient>();
        services.AddSingleton<ServerLogSyncService>();
        services.AddSingleton<FriendlyFireStatsService>();
        services.AddSingleton<ProtectTeammateStatsService>();
        services.AddSingleton<IProtectTeammateDayStatsLoader, ProtectTeammateDayStatsLoader>();
        services.AddSingleton<PlayStatsService>();
        services.AddHostedService<ServerLogTailHostedService>();

        return services;
    }
}
