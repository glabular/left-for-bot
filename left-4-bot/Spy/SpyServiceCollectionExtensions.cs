using left_4_bot.ServerLogs;
using Microsoft.Extensions.DependencyInjection;

namespace left_4_bot.Spy;

/// <summary>
/// Extension methods for registering spy services.
/// </summary>
public static class SpyServiceCollectionExtensions
{
    /// <summary>
    /// Adds spy state, log-tail consumer, and the Telegram forwarder hosted service.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSpy(this IServiceCollection services)
    {
        services.AddSingleton<SpyState>();
        services.AddSingleton<SpyForwarder>();
        services.AddHostedService(sp => sp.GetRequiredService<SpyForwarder>());
        services.AddSingleton<IServerLogTailConsumer, SpyLogTailConsumer>();

        return services;
    }
}
