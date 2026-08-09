using left_4_bot.ServerLogs;

namespace left_4_bot.Rcon;

/// <summary>
/// In-memory numbered lists for the last successful <c>/ban</c> and <c>/unban</c> replies.
/// Lists expire after <see cref="SelectionTtl"/> (lazy check on use).
/// </summary>
public sealed class PlayerModerationSelectionStore
{
    /// <summary>
    /// How long a numbered list stays valid for <c>/ban N</c> / <c>/unban N</c>.
    /// </summary>
    public static readonly TimeSpan SelectionTtl = TimeSpan.FromMinutes(10);

    private readonly object _gate = new();
    private readonly TimeProvider _time;

    private IReadOnlyList<RecentPlayerEntry>? _banCandidates;
    private DateTimeOffset _banCreatedAt;

    private IReadOnlyList<UnbanSelectionEntry>? _unbanEntries;
    private DateTimeOffset _unbanCreatedAt;

    public PlayerModerationSelectionStore()
        : this(TimeProvider.System)
    {
    }

    public PlayerModerationSelectionStore(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>
    /// Replaces the ban candidate list shown after a successful empty <c>/ban</c>.
    /// </summary>
    public void SetBanCandidates(IReadOnlyList<RecentPlayerEntry> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        lock (_gate)
        {
            _banCandidates = candidates;
            _banCreatedAt = _time.GetUtcNow();
        }
    }

    /// <summary>
    /// Replaces the unban list shown after a successful empty <c>/unban</c>.
    /// </summary>
    public void SetUnbanEntries(IReadOnlyList<UnbanSelectionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        lock (_gate)
        {
            _unbanEntries = entries;
            _unbanCreatedAt = _time.GetUtcNow();
        }
    }

    /// <summary>
    /// Resolves a 1-based index from the last ban candidate list.
    /// </summary>
    public bool TryGetBanCandidate(
        int oneBasedIndex,
        out RecentPlayerEntry? entry,
        out string errorMessage)
    {
        lock (_gate)
        {
            if (!EnsureFresh(ref _banCandidates, _banCreatedAt, "ban", out errorMessage))
            {
                entry = null;
                return false;
            }

            return TryGetIndexed(_banCandidates, oneBasedIndex, "ban", out entry, out errorMessage);
        }
    }

    /// <summary>
    /// Resolves a 1-based index from the last unban list.
    /// </summary>
    public bool TryGetUnbanEntry(
        int oneBasedIndex,
        out UnbanSelectionEntry? entry,
        out string errorMessage)
    {
        lock (_gate)
        {
            if (!EnsureFresh(ref _unbanEntries, _unbanCreatedAt, "unban", out errorMessage))
            {
                entry = null;
                return false;
            }

            return TryGetIndexed(_unbanEntries, oneBasedIndex, "unban", out entry, out errorMessage);
        }
    }

    /// <summary>
    /// Gets the current ban candidate count (0 when no list or expired).
    /// </summary>
    public int BanCandidateCount
    {
        get
        {
            lock (_gate)
            {
                if (!EnsureFresh(ref _banCandidates, _banCreatedAt, "ban", out _))
                {
                    return 0;
                }

                return _banCandidates?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// Gets the current unban entry count (0 when no list or expired).
    /// </summary>
    public int UnbanEntryCount
    {
        get
        {
            lock (_gate)
            {
                if (!EnsureFresh(ref _unbanEntries, _unbanCreatedAt, "unban", out _))
                {
                    return 0;
                }

                return _unbanEntries?.Count ?? 0;
            }
        }
    }

    private bool EnsureFresh<T>(
        ref IReadOnlyList<T>? list,
        DateTimeOffset createdAt,
        string commandName,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (list is null)
        {
            return true;
        }

        if (_time.GetUtcNow() - createdAt <= SelectionTtl)
        {
            return true;
        }

        list = null;
        errorMessage =
            $"Сначала отправьте /{commandName} без параметров, чтобы получить пронумерованный список.";
        return false;
    }

    private static bool TryGetIndexed<T>(
        IReadOnlyList<T>? list,
        int oneBasedIndex,
        string commandName,
        out T? entry,
        out string errorMessage)
        where T : class
    {
        entry = null;
        errorMessage = string.Empty;

        if (list is null)
        {
            errorMessage =
                $"Сначала отправьте /{commandName} без параметров, чтобы получить пронумерованный список.";
            return false;
        }

        if (oneBasedIndex < 1)
        {
            errorMessage =
                $"Номер должен быть от 1. Отправьте /{commandName} без параметров, чтобы посмотреть список.";
            return false;
        }

        if (list.Count == 0)
        {
            errorMessage =
                $"Список пуст. Отправьте /{commandName} без параметров ещё раз.";
            return false;
        }

        if (oneBasedIndex > list.Count)
        {
            errorMessage =
                $"Номера {oneBasedIndex} нет в списке. "
                + $"Отправьте /{commandName} без параметров, чтобы посмотреть список.";
            return false;
        }

        entry = list[oneBasedIndex - 1];
        return true;
    }
}
