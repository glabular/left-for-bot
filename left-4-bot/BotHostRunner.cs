using left_4_bot.Call;
using left_4_bot.Handlers;
using left_4_bot.L4D2;
using left_4_bot.Persistence;
using left_4_bot.Rcon;
using left_4_bot.ServerLogs;
using left_4_bot.Spy;
using left_4_bot.TelegramClient;
using left_4_bot.Whois;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace left_4_bot;

public static class BotHostRunner
{
    public static async Task RunAsync(BotSettings settings)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddJsonFile(
            "appsettings.Local.json",
            optional: true,
            reloadOnChange: false);
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
        builder.Services.AddSpy();
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<TelegramConnectionMonitor>();
        builder.Services.AddSingleton<ITelegramBotClient>(sp =>
            new ThrottlingTelegramBotClient(
                new TelegramBotClient(settings.Token),
                sp.GetRequiredService<ILogger<ThrottlingTelegramBotClient>>()));
        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddSingleton<UpdateHandler>();
        builder.Services.AddSingleton<BotUsernameProvider>();
        builder.Services.AddSingleton<GroupCommandHandler>();

        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "data", "bot.db");
        builder.Services.AddBotPersistence(dbPath);

        var host = builder.Build();

        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
        };

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        await host.RunAsync();
    }
}
