using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using left_4_bot.L4D2;
using Microsoft.Extensions.Options;

namespace left_4_bot.Call;

/// <summary>
/// Manages the call list in memory and persists changes to appsettings.Local.json.
/// </summary>
public sealed class CallListService
{
    private static readonly JsonDocumentOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        // Keep Cyrillic (and other non-ASCII) readable in appsettings.Local.json instead of \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Matches a valid Telegram username: 5–32 Latin letters, digits, or underscores.
    /// </summary>
    private static readonly Regex UsernamePattern = new(
        @"^[A-Za-z0-9_]{5,32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _projectAppSettingsPath;
    private readonly string _runtimeAppSettingsPath;
    private List<string> _usernames;
    private List<CallListUser> _users;
    private List<string> _phrases;
    private readonly object _sync = new();

    /// <summary>
    /// Initializes the service from <see cref="CallListOptions"/>.
    /// </summary>
    /// <param name="options">Bound call-list configuration.</param>
    public CallListService(IOptions<CallListOptions> options)
    {
        _projectAppSettingsPath = AppSettingsPathResolver.ResolveProjectAppSettingsPath();
        _runtimeAppSettingsPath = AppSettingsPathResolver.ResolveRuntimeAppSettingsPath();
        _usernames = new List<string>(options.Value.Usernames);
        _users = options.Value.Users
            .Select(u => new CallListUser { Id = u.Id, DisplayName = u.DisplayName })
            .ToList();
        _phrases = new List<string>(options.Value.Phrases);
    }

    /// <summary>
    /// Returns the current call-list entries (usernames, then users by id).
    /// </summary>
    /// <returns>A snapshot of entries; empty when the list has no members.</returns>
    public IReadOnlyList<CallListEntry> GetCallListEntries()
    {
        lock (_sync)
        {
            var entries = new List<CallListEntry>(_usernames.Count + _users.Count);

            foreach (var username in _usernames)
            {
                entries.Add(CallListEntry.FromUsername(username));
            }

            foreach (var user in _users)
            {
                entries.Add(CallListEntry.FromUser(user.Id, user.DisplayName));
            }

            return entries;
        }
    }

    /// <summary>
    /// Picks a random phrase from the pool.
    /// </summary>
    /// <returns>A phrase, or <see langword="null"/> if the pool is empty.</returns>
    public string? PickRandomCallPhrase()
    {
        lock (_sync)
        {
            if (_phrases.Count == 0)
            {
                return null;
            }

            return _phrases[RandomNumberGenerator.GetInt32(_phrases.Count)];
        }
    }

    /// <summary>
    /// Adds or removes a Telegram username on the call list and saves the change.
    /// </summary>
    /// <param name="rawUsername">Username with or without a leading <c>@</c>.</param>
    /// <returns>Whether the username was added, removed, invalid, or failed to save.</returns>
    public CallToggleResult TryToggleUsername(string rawUsername)
    {
        if (!TryNormalizeUsername(rawUsername, out var username))
        {
            return CallToggleResult.Invalid;
        }

        lock (_sync)
        {
            var index = _usernames.FindIndex(
                u => u.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var removed = _usernames[index];
                _usernames.RemoveAt(index);

                if (!TrySave())
                {
                    _usernames.Insert(index, removed);
                    return CallToggleResult.SaveFailed;
                }

                return CallToggleResult.Removed;
            }

            _usernames.Add(username);

            if (!TrySave())
            {
                _usernames.RemoveAt(_usernames.Count - 1);
                return CallToggleResult.SaveFailed;
            }

            return CallToggleResult.Added;
        }
    }

    /// <summary>
    /// Adds or removes a user by Telegram id and saves the change.
    /// </summary>
    /// <param name="userId">Telegram numeric user id.</param>
    /// <param name="displayName">Name stored when adding the user.</param>
    /// <param name="label">On remove, the stored display name; otherwise the trimmed <paramref name="displayName"/>.</param>
    /// <returns>Whether the user was added, removed, invalid, or failed to save.</returns>
    public CallToggleResult TryToggleUser(
        long userId,
        string displayName,
        out string label)
    {
        label = displayName;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return CallToggleResult.Invalid;
        }

        lock (_sync)
        {
            var index = _users.FindIndex(u => u.Id == userId);

            if (index >= 0)
            {
                var removed = _users[index];
                label = string.IsNullOrWhiteSpace(removed.DisplayName)
                    ? displayName
                    : removed.DisplayName;
                _users.RemoveAt(index);

                if (!TrySave())
                {
                    _users.Insert(index, removed);
                    return CallToggleResult.SaveFailed;
                }

                return CallToggleResult.Removed;
            }

            _users.Add(new CallListUser
            {
                Id = userId,
                DisplayName = displayName.Trim(),
            });
            label = displayName.Trim();

            if (!TrySave())
            {
                _users.RemoveAt(_users.Count - 1);
                return CallToggleResult.SaveFailed;
            }

            return CallToggleResult.Added;
        }
    }

    /// <summary>
    /// Adds or removes a ping phrase (case-insensitive) and saves the change.
    /// </summary>
    /// <param name="phrase">Phrase to toggle.</param>
    /// <param name="label">The phrase as stored, or as given when adding.</param>
    /// <returns>Whether the phrase was added, removed, invalid, or failed to save.</returns>
    public CallToggleResult TryTogglePhrase(string phrase, out string label)
    {
        label = string.Empty;

        if (string.IsNullOrWhiteSpace(phrase))
        {
            return CallToggleResult.Invalid;
        }

        var trimmed = phrase.Trim();
        label = trimmed;

        lock (_sync)
        {
            var index = _phrases.FindIndex(
                p => p.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var removed = _phrases[index];
                label = removed;
                _phrases.RemoveAt(index);

                if (!TrySave())
                {
                    _phrases.Insert(index, removed);
                    return CallToggleResult.SaveFailed;
                }

                return CallToggleResult.Removed;
            }

            _phrases.Add(trimmed);

            if (!TrySave())
            {
                _phrases.RemoveAt(_phrases.Count - 1);
                return CallToggleResult.SaveFailed;
            }

            return CallToggleResult.Added;
        }
    }

    /// <summary>
    /// Tries to parse a quoted phrase from command arguments.
    /// </summary>
    /// <param name="args">Raw command arguments.</param>
    /// <param name="phrase">Parsed inner phrase when the method returns <see langword="true"/>.</param>
    /// <param name="looksLikeQuotedPhrase">
    /// <see langword="true"/> when <paramref name="args"/> start with <c>"</c>, even if the quotes are invalid.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a non-empty quoted phrase was parsed; otherwise <see langword="false"/>
    /// (treat as username, unless <paramref name="looksLikeQuotedPhrase"/> is <see langword="true"/>).
    /// </returns>
    public static bool TryParseQuotedPhrase(
        string args,
        out string phrase,
        out bool looksLikeQuotedPhrase)
    {
        phrase = string.Empty;
        looksLikeQuotedPhrase = false;

        if (string.IsNullOrWhiteSpace(args))
        {
            return false;
        }

        var trimmed = args.Trim();
        if (!trimmed.StartsWith('"'))
        {
            return false;
        }

        looksLikeQuotedPhrase = true;

        if (trimmed.Length < 2 || !trimmed.EndsWith('"'))
        {
            return false;
        }

        var inner = trimmed[1..^1].Trim();
        if (inner.Length == 0)
        {
            return false;
        }

        // Nested quotes not supported — keep the parser simple.
        if (inner.Contains('"'))
        {
            return false;
        }

        phrase = inner;
        return true;
    }

    /// <summary>
    /// Strips a leading <c>@</c> and validates a Telegram username.
    /// </summary>
    /// <param name="raw">Raw username input.</param>
    /// <param name="username">Normalized username when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="raw"/> is a valid Telegram username.</returns>
    public static bool TryNormalizeUsername(string? raw, out string username)
    {
        username = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 0 || trimmed.Contains(' ') || !UsernamePattern.IsMatch(trimmed))
        {
            return false;
        }

        username = trimmed;

        return true;
    }

    /// <summary>
    /// Writes the current call list to project and runtime appsettings.Local.json files.
    /// </summary>
    /// <returns><see langword="true"/> if the files were written; otherwise <see langword="false"/>.</returns>
    private bool TrySave()
    {
        try
        {
            var readPath = File.Exists(_projectAppSettingsPath)
                ? _projectAppSettingsPath
                : _runtimeAppSettingsPath;

            JsonObject root;

            if (File.Exists(readPath))
            {
                var json = File.ReadAllText(readPath);
                root = JsonNode.Parse(json, documentOptions: JsonReadOptions)?.AsObject()
                    ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var usernamesArray = new JsonArray();
            foreach (var name in _usernames)
            {
                usernamesArray.Add(name);
            }

            var usersArray = new JsonArray();
            foreach (var user in _users)
            {
                usersArray.Add(new JsonObject
                {
                    ["Id"] = user.Id,
                    ["DisplayName"] = user.DisplayName,
                });
            }

            var phrasesArray = new JsonArray();
            foreach (var phrase in _phrases)
            {
                phrasesArray.Add(phrase);
            }

            root[CallListOptions.SectionName] = new JsonObject
            {
                ["Usernames"] = usernamesArray,
                ["Users"] = usersArray,
                ["Phrases"] = phrasesArray,
            };

            var output = root.ToJsonString(JsonWriteOptions);

            File.WriteAllText(_projectAppSettingsPath, output);

            if (!string.Equals(_projectAppSettingsPath, _runtimeAppSettingsPath, StringComparison.OrdinalIgnoreCase)
                && File.Exists(_runtimeAppSettingsPath))
            {
                File.WriteAllText(_runtimeAppSettingsPath, output);
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return false;
        }
    }
}
