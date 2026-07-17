using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using left_4_bot.L4D2;
using Microsoft.Extensions.Options;

namespace left_4_bot.Call;

public sealed class CallListService
{
    private static readonly JsonDocumentOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        // Keep Cyrillic (and other non-ASCII) readable in appsettings.json instead of \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Telegram usernames: 5–32 chars, Latin letters, digits, underscores.
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
    /// Random phrase from the pool, or null if the pool is empty.
    /// </summary>
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
    /// Toggle by Telegram user id. On add, stores <paramref name="displayName"/>.
    /// On remove, <paramref name="label"/> is the name that was stored.
    /// </summary>
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
    /// Toggle a ping phrase. Comparison is case-insensitive.
    /// On success, <paramref name="label"/> is the phrase as stored (or as given on add).
    /// </summary>
    public CallToggleResult TryTogglePhrase(string phrase, out string label)
    {
        label = "";

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
    /// Parses <c>"phrase text"</c> from command args. Returns false if not a quoted phrase form
    /// (caller should treat as username) or if quotes are present but invalid.
    /// </summary>
    /// <param name="looksLikeQuotedPhrase">
    /// True when args start with <c>"</c> — even if invalid (empty / unclosed).
    /// </param>
    public static bool TryParseQuotedPhrase(
        string args,
        out string phrase,
        out bool looksLikeQuotedPhrase)
    {
        phrase = "";
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

    /// <summary>Kept for callers that used the old name.</summary>
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
