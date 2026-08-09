using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.Rcon;

public static class RconServiceCollectionExtensions
{
    public static IServiceCollection AddRcon(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RconOptions>(configuration.GetSection(RconOptions.SectionName));
        services.AddSingleton<ServerRconService>();
        services.AddSingleton<ServerRestartService>();
        services.AddSingleton<ServerBanService>();
        services.AddSingleton<PlayerModerationSelectionStore>();

        return services;
    }
}
