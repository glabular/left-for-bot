using System.Text;
using left_4_bot.ServerLogs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace left_4_bot.Whois;

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

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var download = await _logSync.DownloadTodayLogFilesAsync(cancellationToken);
        if (!download.IsSuccess)
        {
            _logger.LogDebug("Whois log sync: {Message}", download.Message);
            return;
        }

        var enteredThisTick = new List<WhoisPlayerRef>();

        foreach (var path in download.LocalFilePaths)
        {
            var activities = ReadNewActivities(path);
            if (activities.Count == 0)
            {
                continue;
            }

            _sessionTracker.ApplyActivities(activities);

            foreach (var activity in activities)
            {
                if (activity.Kind == WhoisLogActivityKind.Entered)
                {
                    enteredThisTick.Add(activity.Player);
                }
            }
        }

        PruneOffsets(download.LocalFilePaths);

        if (enteredThisTick.Count == 0 || !_state.Enabled)
        {
            return;
        }

        if (_botSettings.GroupChatId == 0)
        {
            _logger.LogDebug("Whois skipped send: GroupChatId is 0");
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

        var filtered = _whoisService.FilterNonPriority(enteredThisTick);
        var toAnnounce = _sessionTracker.TakeUnannounced(filtered);
        if (toAnnounce.Count == 0)
        {
            return;
        }

        await _whoisService.ReportPlayersAsync(
            _bot,
            _botSettings.GroupChatId,
            toAnnounce,
            cancellationToken);

        _sessionTracker.MarkAnnounced(toAnnounce.Select(p => p.ClassicSteamId));
    }

    private IReadOnlyList<WhoisLogActivity> ReadNewActivities(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var length = new FileInfo(filePath).Length;

        if (!_offsets.TryGetValue(filePath, out var offset))
        {
            // First attach: seek EOF so we never replay history.
            _offsets[filePath] = length;
            return [];
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
