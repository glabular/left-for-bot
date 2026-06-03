using left_4_bot;
using left_4_bot.Console;
using left_4_bot.Storage;
using Microsoft.Extensions.Configuration.UserSecrets;
using System.Reflection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var userSecretsId = typeof(Program).Assembly
            .GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId
            ?? throw new InvalidOperationException(
                "UserSecretsId is not configured. Add it to left-4-bot.csproj.");

        var store = new UserSecretsSettingsStore(userSecretsId);
        var app = new BotConsoleApp(store);

        while (true)
        {
            var action = app.Run();

            if (action == ConsoleAction.Exit)
            {
                break;
            }

            if (action == ConsoleAction.StartBot)
            {
                if (!store.HasToken())
                {
                    Console.WriteLine("Token required. Configure it first.");
                    continue;
                }

                var settings = store.Load();

                await BotHostRunner.RunAsync(settings);
            }
        }
    }
}