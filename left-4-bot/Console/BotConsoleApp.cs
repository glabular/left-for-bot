using left_4_bot.Storage;

namespace left_4_bot.Console;

public sealed class BotConsoleApp
{
    private readonly UserSecretsSettingsStore _store;

    public BotConsoleApp(UserSecretsSettingsStore store)
    {
        _store = store;
    }

    public ConsoleAction Run()
    {
        while (true)
        {
            if (!_store.HasBotToken())
            {
                if (!ShowNoTokenMenu())
                {
                    continue;
                }
            }
            else
            {
                var action = ShowMainMenu();
                if (action != null)
                {
                    return action.Value;
                }
            }
        }
    }

    private bool ShowNoTokenMenu()
    {
        System.Console.WriteLine();
        System.Console.WriteLine("No bot token configured.");
        System.Console.WriteLine("1) Set bot token");
        System.Console.WriteLine("0) Exit");

        switch (System.Console.ReadLine()?.Trim())
        {
            case "1":
                PromptAndSaveBotToken();
                return false;
            case "0":
                Environment.Exit(0);
                return false;
            default:
                System.Console.WriteLine("Set a bot token before using this app.");
                return false;
        }
    }

    private ConsoleAction? ShowMainMenu()
    {
        var settings = _store.Load();

        System.Console.WriteLine();
        var steamWebApiKey = _store.LoadSteamWebApiKey();

        System.Console.WriteLine($"Token: {ConsolePrompts.MaskSecret(settings.Token)}");
        System.Console.WriteLine($"Group chat ID: {settings.GroupChatId}");
        System.Console.WriteLine($"Steam Web API key: {ConsolePrompts.MaskSecret(steamWebApiKey)}");
        System.Console.WriteLine("1) Start bot");
        System.Console.WriteLine("2) Change bot token");
        System.Console.WriteLine("3) Set group chat ID (0 = discovery mode)");
        System.Console.WriteLine("4) Set Steam Web API key");
        System.Console.WriteLine("0) Exit");

        return System.Console.ReadLine()?.Trim() switch
        {
            "1" => ConsoleAction.StartBot,
            "2" => PromptAndSaveBotTokenAndContinue(),
            "3" => PromptAndSaveGroupChatIdAndContinue(),
            "4" => PromptAndSaveSteamWebApiKeyAndContinue(),
            "0" => ConsoleAction.Exit,
            _ => InvalidChoice()
        };
    }

    private ConsoleAction? PromptAndSaveBotTokenAndContinue()
    {
        PromptAndSaveBotToken();
        return null;
    }

    private ConsoleAction? PromptAndSaveGroupChatIdAndContinue()
    {
        PromptAndSaveGroupChatId();
        return null;
    }

    private ConsoleAction? PromptAndSaveSteamWebApiKeyAndContinue()
    {
        PromptAndSaveSteamWebApiKey();
        return null;
    }

    private ConsoleAction? InvalidChoice()
    {
        System.Console.WriteLine("Invalid choice.");
        return null;
    }

    private void PromptAndSaveBotToken()
    {
        var token = ConsolePrompts.ReadLine("Enter bot token: ");

        if (string.IsNullOrWhiteSpace(token))
        {
            System.Console.WriteLine("Token cannot be empty.");

            return;
        }

        var current = _store.Load();
        _store.Save(new BotSettings
        {
            Token = token,
            GroupChatId = current.GroupChatId
        });

        System.Console.WriteLine("Token saved.");
    }

    private void PromptAndSaveGroupChatId()
    {
        var groupChatId = ConsolePrompts.ReadLong("Enter group chat ID (0 for discovery): ");
        var current = _store.Load();

        _store.Save(new BotSettings
        {
            Token = current.Token,
            GroupChatId = groupChatId
        });

        System.Console.WriteLine("Group chat ID saved.");
    }

    private void PromptAndSaveSteamWebApiKey()
    {
        var apiKey = ConsolePrompts.ReadLine("Enter Steam Web API key (empty to clear): ");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _store.SaveSteamWebApiKey(apiKey.Trim());
            System.Console.WriteLine("Steam Web API key saved.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_store.LoadSteamWebApiKey()))
        {
            System.Console.WriteLine("Steam Web API key is already not set.");
            return;
        }

        var confirm = ConsolePrompts.ReadLine("Clear Steam Web API key? (y/n): ");
        if (!confirm.Equals("y", StringComparison.OrdinalIgnoreCase)
            && !confirm.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            System.Console.WriteLine("Cancelled.");
            return;
        }

        _store.ClearSteamWebApiKey();
        System.Console.WriteLine("Steam Web API key cleared. Restart the bot if it is running.");
    }
}
