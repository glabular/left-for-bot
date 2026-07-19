using System.Text;
using left_4_bot.ServerLogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace left_4_bot.Whois;

/// <summary>
/// Background service that polls server logs and announces new player joins.
/// </summary>
public sealed class WhoisLogWatcherHostedService : BackgroundService
{
    private readonly ServerLogSyncService _logSync;
    private readonly WhoisState _state;
    private readonly WhoisService _whoisService;
    private readonly WhoisSessionTracker _sessionTracker;
    private readonly BotSettings _botSettings;
    private readonly ITelegramBotClient _bot;
    private readonly IOptions<WhoisOptions> _options;
    private readonly ILogger<WhoisLogWatcherHostedService> _logger;

    private readonly Dictionary<string, long> _offsets = new(StringComparer.OrdinalIgnoreCase);
    private bool _missingApiKeyNotified;

    // True once the first successful poll after enabling has seeked all current files to EOF.
    // Before priming we skip history; after priming, a newly rotated-in file is read from the start.
    private bool _primed;

    // Tracks whether the last log download failed, so we log the failure/recovery only on the
    // transition instead of spamming a warning every poll while FTP is down.
    private bool _downloadFailing;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhoisLogWatcherHostedService"/> class.
    /// </summary>
    /// <param name="logSync">Service used to download active server log files.</param>
    /// <param name="state">Shared whois enabled state.</param>
    /// <param name="whoisService">Service used to send whois reports.</param>
    /// <param name="sessionTracker">Tracker for online and announced players.</param>
    /// <param name="botSettings">Bot settings, including the group chat ID.</param>
    /// <param name="bot">Telegram bot client.</param>
    /// <param name="options">Whois configuration options.</param>
    /// <param name="logger">Logger for this service.</param>
    public WhoisLogWatcherHostedService(
        ServerLogSyncService logSync,
        WhoisState state,
        WhoisService whoisService,
        WhoisSessionTracker sessionTracker,
        BotSettings botSettings,
        ITelegramBotClient bot,
        IOptions<WhoisOptions> options,
        ILogger<WhoisLogWatcherHostedService> logger)
    {
        _logSync = logSync;
        _state = state;
        _whoisService = whoisService;
        _sessionTracker = sessionTracker;
        _botSettings = botSettings;
        _bot = bot;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(5, _options.Value.PollIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation(
            "Whois log watcher started (interval {IntervalSeconds}s, enabled={Enabled})",
            intervalSeconds,
            _state.Enabled);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Whois log poll failed");
            }
        }
    }

    /// <summary>
    /// Performs a single log poll and announces newly entered players when needed.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the poll.</param>
    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // When whois is off (or there is no group to notify), do not touch FTP at all.
        // Reset offsets so that when it is re-enabled the first poll re-seeks to EOF and never
        // replays historical log lines.
        if (!_state.Enabled || _botSettings.GroupChatId == 0)
        {
            if (_offsets.Count > 0)
            {
                _offsets.Clear();
            }

            _missingApiKeyNotified = false;
            _downloadFailing = false;
            _primed = false;

            return;
        }

        var download = await _logSync.DownloadActiveLogFilesAsync(cancellationToken);

        if (!download.IsSuccess)
        {
            if (!_downloadFailing)
            {
                _downloadFailing = true;
                _logger.LogWarning("Whois log sync failed: {Message}", download.Message);
            }

            return;
        }

        if (_downloadFailing)
        {
            _downloadFailing = false;
            _logger.LogInformation("Whois log sync recovered");
        }

        var priming = !_primed;
        var enteredThisTick = new List<WhoisPlayerRef>();

        foreach (var path in download.LocalFilePaths)
        {
            var activities = ReadNewActivities(path, priming);

            if (activities.Count == 0)
            {
                continue;
            }

            _sessionTracker.UpdateOnlinePlayersFromLog(activities);

            foreach (var activity in activities)
            {
                if (activity.Kind == WhoisLogActivityKind.Entered)
                {
                    enteredThisTick.Add(activity.Player);
                }
            }
        }

        PruneOffsets(download.LocalFilePaths);

        // Only consider ourselves primed once we have actually anchored to at least one file, so an
        // early empty poll (server idle / no active log yet) does not later replay history.
        if (priming && download.LocalFilePaths.Count > 0)
        {
            _primed = true;
        }

        if (enteredThisTick.Count == 0)
        {
            return;
        }

        if (!_whoisService.HasApiKey)
        {
            if (!_missingApiKeyNotified)
            {
                _missingApiKeyNotified = true;
                _logger.LogWarning("Whois Steam Web API key is not configured");
                await _bot.SendMessage(
                    _botSettings.GroupChatId,
                    WhoisService.MissingApiKeyMessage,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        _missingApiKeyNotified = false;

        var filtered = _whoisService.ExcludePriorityPlayers(enteredThisTick);
        var toAnnounce = _sessionTracker.FilterUnannouncedPlayers(filtered);

        if (toAnnounce.Count == 0)
        {
            return;
        }

        await _whoisService.SendWhoisReportAsync(
            _bot,
            _botSettings.GroupChatId,
            toAnnounce,
            cancellationToken);

        _sessionTracker.MarkAnnounced(toAnnounce.Select(p => p.ClassicSteamId));
    }

    /// <summary>
    /// Reads and parses new activity from a log file since the last known offset.
    /// </summary>
    /// <param name="filePath">Local path of the log file.</param>
    /// <param name="priming">
    /// <see langword="true"/> to seek to EOF without parsing historical lines.
    /// </param>
    /// <returns>Parsed activities from the new file slice; empty when priming or unchanged.</returns>
    private IReadOnlyList<WhoisLogActivity> ReadNewActivities(string filePath, bool priming)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var length = new FileInfo(filePath).Length;

        if (priming)
        {
            // First poll after enabling: seek every current file to EOF so we never replay history.
            _offsets[filePath] = length;
            return [];
        }

        if (!_offsets.TryGetValue(filePath, out var offset))
        {
            // A file we have not seen before appearing mid-session means the server rotated to a
            // new log. Read it from the start so joins written right after rotation are not missed.
            offset = 0;
        }

        if (length < offset)
        {
            // File rotated/truncated — start from beginning of new content.
            offset = 0;
        }

        if (length == offset)
        {
            return [];
        }

        var slice = ReadSlice(filePath, offset, length);
        _offsets[filePath] = length;
        return WhoisLogActivityParser.Parse(slice);
    }

    /// <summary>
    /// Reads a UTF-8 byte range from a log file.
    /// </summary>
    /// <param name="filePath">Local path of the log file.</param>
    /// <param name="offset">Start byte offset.</param>
    /// <param name="length">Current file length used as the exclusive end offset.</param>
    /// <returns>Decoded text for the requested slice.</returns>
    private static string ReadSlice(string filePath, long offset, long length)
    {
        var count = (int)(length - offset);
        var buffer = new byte[count];

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        stream.Seek(offset, SeekOrigin.Begin);
        var read = stream.Read(buffer, 0, count);

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// Removes offset entries for log files that are no longer active.
    /// </summary>
    /// <param name="activePaths">Paths of currently active log files.</param>
    private void PruneOffsets(IReadOnlyList<string> activePaths)
    {
        var active = activePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = _offsets.Keys.Where(path => !active.Contains(path)).ToList();

        foreach (var path in stale)
        {
            _offsets.Remove(path);
        }
    }
}
