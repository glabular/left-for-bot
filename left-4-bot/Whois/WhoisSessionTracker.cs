using left_4_bot.ServerLogs;
using Microsoft.Extensions.Options;

namespace left_4_bot.Whois;

/// <summary>
/// Tracks who is online (from log enter/disconnect) and who was already announced
/// this session. Announced set clears when the server empties or the gaming day changes.
/// </summary>
public sealed class WhoisSessionTracker
{
    private readonly object _gate = new();
    private readonly HashSet<string> _onlineSteamIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _announcedSteamIds = new(StringComparer.Ordinal);
    private readonly IOptions<GamingDayOptions> _gamingDayOptions;
    private DateOnly? _gamingDayAnchor;

    public WhoisSessionTracker(IOptions<GamingDayOptions> gamingDayOptions)
    {
        _gamingDayOptions = gamingDayOptions;
    }

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
