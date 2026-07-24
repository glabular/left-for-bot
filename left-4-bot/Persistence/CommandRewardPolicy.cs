namespace left_4_bot.Persistence;

/// <summary>
/// Decides how many Tabs a group command earns.
/// </summary>
public static class CommandRewardPolicy
{
    /// <summary>
    /// Base Tabs when args are empty. Missing command or 0 = no reward.
    /// <c>restart</c> is not listed here: it is args-only (see <see cref="TryResolve"/>).
    /// </summary>
    private static readonly Dictionary<string, int> BaseTabsByCommand = new(StringComparer.OrdinalIgnoreCase)
    {
        ["help"] = 0,
        ["link"] = 0,
        ["balance"] = 0,

        ["map"] = ToteEconomy.CommandRewardTabs,
        ["favmaps"] = ToteEconomy.CommandRewardTabs,
        ["call"] = ToteEconomy.CommandRewardTabs,
        ["stat"] = ToteEconomy.CommandRewardTabs,
        ["status"] = ToteEconomy.CommandRewardTabs,
        ["server"] = ToteEconomy.CommandRewardTabs,
        ["whois"] = ToteEconomy.CommandRewardTabs,
        ["spy"] = ToteEconomy.CommandRewardTabs,
    };

    /// <summary>
    /// Resolves Tab amount for a handled command. Returns 0 when no reward.
    /// </summary>
    /// <param name="command">Command name without slash (e.g. <c>stat</c>).</param>
    /// <param name="args">Text after the command; whitespace-only counts as empty.</param>
    public static int Resolve(string command, string args)
    {
        var hasArgs = !string.IsNullOrWhiteSpace(args);

        // /restart: no reward without args.
        if (command.Equals("restart", StringComparison.OrdinalIgnoreCase))
        {
            return hasArgs ? ToteEconomy.CommandRewardWithArgsTabs : 0;
        }

        if (!BaseTabsByCommand.TryGetValue(command, out var baseTabs) || baseTabs <= 0)
        {
            return 0;
        }

        return hasArgs ? ToteEconomy.CommandRewardWithArgsTabs : baseTabs;
    }
}
