using left_4_bot.ServerLogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.Whois;

/// <summary>
/// Extension methods for registering whois services.
/// </summary>
public static class WhoisServiceCollectionExtensions
{
    /// <summary>
    /// Adds whois options, services, and the shared log-tail consumer.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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
        services.AddSingleton<IServerLogTailConsumer, WhoisLogTailConsumer>();

        return services;
    }
}
