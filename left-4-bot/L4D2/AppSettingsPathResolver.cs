namespace left_4_bot.L4D2;

internal static class AppSettingsPathResolver
{
    private const string FileName = "appsettings.Local.json";

    public static string ResolveProjectAppSettingsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return Path.Combine(directory.FullName, FileName);
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, FileName);
    }

    public static string ResolveRuntimeAppSettingsPath() =>
        Path.Combine(AppContext.BaseDirectory, FileName);
}
