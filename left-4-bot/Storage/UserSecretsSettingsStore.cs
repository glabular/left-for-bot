using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace left_4_bot.Storage;

public sealed class UserSecretsSettingsStore
{
    private readonly string _userSecretsId;

    public UserSecretsSettingsStore(string userSecretsId)
    {
        _userSecretsId = userSecretsId;
    }

    public BotSettings Load()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(_userSecretsId)
            .Build();

        return config.GetSection("Bot").Get<BotSettings>() ?? new BotSettings();
    }

    public void Save(BotSettings settings)
    {
        var path = PathHelper.GetSecretsPathFromSecretsId(_userSecretsId);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        Dictionary<string, string?> secrets;

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            secrets = JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
                ?? [];
        }
        else
        {
            secrets = [];
        }

        secrets["Bot:Token"] = settings.Token;
        secrets["Bot:GroupChatId"] = settings.GroupChatId.ToString();

        var output = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(path, output);
    }

    public bool HasToken() => !string.IsNullOrWhiteSpace(Load().Token);
}
