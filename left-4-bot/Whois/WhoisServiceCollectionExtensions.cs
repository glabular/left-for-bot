using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.Whois;

public static class WhoisServiceCollectionExtensions
{
    public static IServiceCollection AddWhois(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WhoisOptions>(configuration.GetSection(WhoisOptions.SectionName));
        services.AddSingleton<WhoisState>();
        services.AddSingleton<WhoisSessionTracker>();
        services.AddHttpClient(nameof(SteamPlayerProfileService));
        services.AddSingleton<SteamPlayerProfileService>();
        services.AddSingleton<WhoisService>();
        services.AddHostedService<WhoisLogWatcherHostedService>();

        return services;
    }
}
