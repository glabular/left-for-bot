using left_4_bot.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace left_4_bot;

public static class BotHostRunner
{
    public static async Task RunAsync(BotSettings settings)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddUserSecrets(typeof(Program).Assembly);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(settings.Token));
        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddTransient<UpdateHandler>();
        builder.Services.AddSingleton<BotUsernameProvider>();
        builder.Services.AddTransient<GroupCommandHandler>();

        var host = builder.Build();

        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
        };

        await host.RunAsync();
    }
}
