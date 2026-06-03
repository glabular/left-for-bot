using System.Reflection;
using left_4_bot.Console;
using left_4_bot.Storage;
using Microsoft.Extensions.Configuration.UserSecrets;

internal class Program
{
    private static void Main(string[] args)
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
                break;

            if (action == ConsoleAction.StartBot)
            {
                Console.WriteLine("Bot host not configured yet. (Added in Step 10.)");
            }
        }
    }
}