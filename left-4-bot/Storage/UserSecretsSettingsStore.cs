using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace left_4_bot.Storage;

public sealed class UserSecretsSettingsStore
{
    private const string SteamWebApiKeySecret = "Whois:SteamWebApiKey";

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

    public string LoadSteamWebApiKey()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(_userSecretsId)
            .Build();

        return config[SteamWebApiKeySecret] ?? string.Empty;
    }

    public void Save(BotSettings settings)
    {
        var secrets = LoadSecretsDictionary();

        secrets["Bot:Token"] = settings.Token;
        secrets["Bot:GroupChatId"] = settings.GroupChatId.ToString();

        WriteSecrets(secrets);
    }

    public void SaveSteamWebApiKey(string apiKey)
    {
        var secrets = LoadSecretsDictionary();
        secrets[SteamWebApiKeySecret] = apiKey;
        WriteSecrets(secrets);
    }

    public void ClearSteamWebApiKey()
    {
        var secrets = LoadSecretsDictionary();
        secrets.Remove(SteamWebApiKeySecret);
        WriteSecrets(secrets);
    }

    public bool HasBotToken() => !string.IsNullOrWhiteSpace(Load().Token);

    private Dictionary<string, string?> LoadSecretsDictionary()
    {
        var path = PathHelper.GetSecretsPathFromSecretsId(_userSecretsId);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        if (!File.Exists(path))
        {
            return [];
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? [];
    }

    private void WriteSecrets(Dictionary<string, string?> secrets)
    {
        var path = PathHelper.GetSecretsPathFromSecretsId(_userSecretsId);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var output = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, output);
    }
}
