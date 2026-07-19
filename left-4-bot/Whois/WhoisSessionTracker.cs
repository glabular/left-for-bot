using left_4_bot.ServerLogs;
using Microsoft.Extensions.Options;

namespace left_4_bot.Whois;

/// <summary>
/// Tracks online players and who was already announced this session.
/// </summary>
public sealed class WhoisSessionTracker
{
    private readonly object _gate = new();
    private readonly HashSet<string> _onlineSteamIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _announcedSteamIds = new(StringComparer.Ordinal);
    private readonly IOptions<GamingDayOptions> _gamingDayOptions;
    private DateOnly? _gamingDayAnchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhoisSessionTracker"/> class.
    /// </summary>
    /// <param name="gamingDayOptions">Gaming-day options used to reset announcements.</param>
    public WhoisSessionTracker(IOptions<GamingDayOptions> gamingDayOptions)
    {
        _gamingDayOptions = gamingDayOptions;
    }

    /// <summary>
    /// Updates the online-player set from log enter and disconnect activities.
    /// </summary>
    /// <param name="activities">Parsed log activities for the current poll.</param>
    internal void UpdateOnlinePlayersFromLog(IReadOnlyList<WhoisLogActivity> activities)
    {
        lock (_gate)
        {
            ResetAnnouncedIfGamingDayChanged();

            foreach (var activity in activities)
            {
                var steamId = activity.Player.ClassicSteamId;

                if (activity.Kind == WhoisLogActivityKind.Disconnected)
                {
                    _onlineSteamIds.Remove(steamId);
                    if (_onlineSteamIds.Count == 0)
                    {
                        _announcedSteamIds.Clear();
                    }

                    continue;
                }

                _onlineSteamIds.Add(steamId);
            }
        }
    }

    /// <summary>
    /// Returns candidates that have not been announced yet in this session.
    /// </summary>
    /// <param name="candidates">Players considered for announcement.</param>
    /// <returns>Unannounced unique players.</returns>
    public IReadOnlyList<WhoisPlayerRef> FilterUnannouncedPlayers(IReadOnlyList<WhoisPlayerRef> candidates)
    {
        lock (_gate)
        {
            ResetAnnouncedIfGamingDayChanged();

            var result = new List<WhoisPlayerRef>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var candidate in candidates)
            {
                if (!seen.Add(candidate.ClassicSteamId))
                {
                    continue;
                }

                if (_announcedSteamIds.Contains(candidate.ClassicSteamId))
                {
                    continue;
                }

                result.Add(candidate);
            }

            return result;
        }
    }

    /// <summary>
    /// Marks players as already announced for the current session.
    /// </summary>
    /// <param name="classicSteamIds">Classic Steam IDs to mark.</param>
    public void MarkAnnounced(IEnumerable<string> classicSteamIds)
    {
        lock (_gate)
        {
            ResetAnnouncedIfGamingDayChanged();

            foreach (var steamId in classicSteamIds)
            {
                if (!string.IsNullOrWhiteSpace(steamId))
                {
                    _announcedSteamIds.Add(steamId);
                }
            }
        }
    }

    /// <summary>
    /// Clears announced players when the gaming-day anchor changes.
    /// </summary>
    private void ResetAnnouncedIfGamingDayChanged()
    {
        var anchor = GamingDay.GetCurrentAnchor(_gamingDayOptions.Value);

        if (_gamingDayAnchor == anchor)
        {
            return;
        }

        _gamingDayAnchor = anchor;
        _announcedSteamIds.Clear();
    }
}
