using left_4_bot.Call;
using left_4_bot.Handlers;
using left_4_bot.L4D2;
using left_4_bot.Rcon;
using left_4_bot.ServerLogs;
using left_4_bot.Whois;
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
        builder.Services.Configure<GoldenPoolOptions>(
            builder.Configuration.GetSection("GoldenPool"));
        builder.Services.AddSingleton<GoldenPoolService>();
        builder.Services.AddSingleton<GoldenPoolEditLimiter>();
        builder.Services.Configure<CallListOptions>(
            builder.Configuration.GetSection(CallListOptions.SectionName));
        builder.Services.AddSingleton<CallListService>();
        builder.Services.AddServerLogSync(builder.Configuration);
        builder.Services.AddRcon(builder.Configuration);
        builder.Services.AddWhois(builder.Configuration);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<TelegramConnectionMonitor>();
        builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(settings.Token));
        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddSingleton<UpdateHandler>();
        builder.Services.AddSingleton<BotUsernameProvider>();
        builder.Services.AddSingleton<GroupCommandHandler>();

        var host = builder.Build();

        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
        };

        await host.RunAsync();
    }
}
