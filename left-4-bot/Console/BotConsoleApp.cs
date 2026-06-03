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
            if (!_store.HasToken())
            {
                ShowNoTokenMenu();
            }
            else
            {
                var action = ShowMainMenu();
                if (action != null)
                    return action.Value;
            }
        }
    }

    private void ShowNoTokenMenu()
    {
        System.Console.WriteLine();
        System.Console.WriteLine("No bot token configured.");
        System.Console.WriteLine("1) Set bot token");
        System.Console.WriteLine("0) Exit");

        switch (System.Console.ReadLine()?.Trim())
        {
            case "1":
                SetToken();
                break;
            case "0":
                Environment.Exit(0);
                break;
            default:
                System.Console.WriteLine("Set a bot token before using this app.");
                break;
        }
    }

    private ConsoleAction? ShowMainMenu()
    {
        var settings = _store.Load();

        System.Console.WriteLine();
        System.Console.WriteLine($"Token: {ConsolePrompts.MaskToken(settings.Token)}");
        System.Console.WriteLine($"Group chat ID: {settings.GroupChatId}");
        System.Console.WriteLine("1) Start bot");
        System.Console.WriteLine("2) Change bot token");
        System.Console.WriteLine("3) Set group chat ID (0 = discovery mode)");
        System.Console.WriteLine("0) Exit");

        return System.Console.ReadLine()?.Trim() switch
        {
            "1" => ConsoleAction.StartBot,
            "2" => SetTokenAndContinue(),
            "3" => SetGroupChatIdAndContinue(),
            "0" => ConsoleAction.Exit,
            _ => InvalidChoice()
        };
    }

    private ConsoleAction? SetTokenAndContinue()
    {
        SetToken();
        return null;
    }

    private ConsoleAction? SetGroupChatIdAndContinue()
    {
        SetGroupChatId();
        return null;
    }

    private ConsoleAction? InvalidChoice()
    {
        System.Console.WriteLine("Invalid choice.");
        return null;
    }

    private void SetToken()
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

    private void SetGroupChatId()
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
}