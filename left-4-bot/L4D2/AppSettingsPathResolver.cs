namespace left_4_bot.L4D2;

internal static class AppSettingsPathResolver
{
    public static string ResolveProjectAppSettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return Path.Combine(directory.FullName, "appsettings.json");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public static string ResolveRuntimeAppSettingsPath() =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");
}
