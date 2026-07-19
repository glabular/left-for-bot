using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.ServerLogs;

/// <summary>
/// Background service that polls active server logs over FTP and fans out new byte slices
/// to registered <see cref="IServerLogTailConsumer"/> instances.
/// </summary>
public sealed class ServerLogTailHostedService : BackgroundService
{
    private readonly ServerLogSyncService _logSync;
    private readonly IEnumerable<IServerLogTailConsumer> _consumers;
    private readonly BotSettings _botSettings;
    private readonly IOptions<ServerLogTailOptions> _options;
    private readonly ILogger<ServerLogTailHostedService> _logger;

    private readonly Dictionary<string, long> _offsets = new(StringComparer.OrdinalIgnoreCase);

    // True once the first successful poll after enabling has seeked all current files to EOF.
    // Before priming we skip history; after priming, a newly rotated-in file is read from the start.
    private bool _primed;

    // Tracks whether the last log download failed, so we log the failure/recovery only on the
    // transition instead of spamming a warning every poll while FTP is down.
    private bool _downloadFailing;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerLogTailHostedService"/> class.
    /// </summary>
    /// <param name="logSync">Service used to download active server log files.</param>
    /// <param name="consumers">Features that react to near-real-time log slices.</param>
    /// <param name="botSettings">Bot settings, including the group chat ID.</param>
    /// <param name="options">Log-tail configuration options.</param>
    /// <param name="logger">Logger for this service.</param>
    public ServerLogTailHostedService(
        ServerLogSyncService logSync,
        IEnumerable<IServerLogTailConsumer> consumers,
        BotSettings botSettings,
        IOptions<ServerLogTailOptions> options,
        ILogger<ServerLogTailHostedService> logger)
    {
        _logSync = logSync;
        _consumers = consumers;
        _botSettings = botSettings;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(5, _options.Value.PollIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        _logger.LogInformation(
            "Server log tail started (interval {IntervalSeconds}s)",
            intervalSeconds);

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
                _logger.LogWarning(ex, "Server log tail poll failed");
            }
        }
    }

    /// <summary>
    /// Performs a single poll: download active logs when any consumer wants them, then fan out slices.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the poll.</param>
    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // When nobody needs the tail (or there is no group to notify), do not touch FTP at all.
        // Reset offsets so that when polling resumes the first tick re-seeks to EOF and never
        // replays historical log lines.
        if (_botSettings.GroupChatId == 0 || !AnyConsumerWantsPolling())
        {
            ResetPollingState();

            return;
        }

        var download = await _logSync.DownloadActiveLogFilesAsync(cancellationToken);

        if (!download.IsSuccess)
        {
            if (!_downloadFailing)
            {
                _downloadFailing = true;
                _logger.LogWarning("Server log tail sync failed: {Message}", download.Message);
            }

            return;
        }

        if (_downloadFailing)
        {
            _downloadFailing = false;
            _logger.LogInformation("Server log tail sync recovered");
        }

        var priming = !_primed;
        var slices = new List<ServerLogTailSlice>();

        foreach (var path in download.LocalFilePaths)
        {
            var slice = ReadNewSlice(path, priming);

            if (slice is not null)
            {
                slices.Add(slice);
            }
        }

        PruneOffsets(download.LocalFilePaths);

        // Only consider ourselves primed once we have actually anchored to at least one file, so an
        // early empty poll (server idle / no active log yet) does not later replay history.
        if (priming && download.LocalFilePaths.Count > 0)
        {
            _primed = true;
        }

        if (slices.Count == 0)
        {
            return;
        }

        foreach (var consumer in _consumers)
        {
            if (!consumer.WantsPolling)
            {
                continue;
            }

            try
            {
                await consumer.OnNewSlicesAsync(slices, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Server log tail consumer {Consumer} failed",
                    consumer.GetType().Name);
            }
        }
    }

    private bool AnyConsumerWantsPolling()
    {
        foreach (var consumer in _consumers)
        {
            if (consumer.WantsPolling)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetPollingState()
    {
        if (_offsets.Count > 0)
        {
            _offsets.Clear();
        }

        _downloadFailing = false;
        _primed = false;
    }

    /// <summary>
    /// Reads new bytes from a log file since the last known offset.
    /// </summary>
    /// <param name="filePath">Local path of the log file.</param>
    /// <param name="priming">
    /// <see langword="true"/> to seek to EOF without returning historical text.
    /// </param>
    /// <returns>A slice when new bytes exist; otherwise <see langword="null"/>.</returns>
    private ServerLogTailSlice? ReadNewSlice(string filePath, bool priming)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var length = new FileInfo(filePath).Length;

        if (priming)
        {
            // First poll after enabling: seek every current file to EOF so we never replay history.
            _offsets[filePath] = length;

            return null;
        }

        if (!_offsets.TryGetValue(filePath, out var offset))
        {
            // A file we have not seen before appearing mid-session means the server rotated to a
            // new log. Read it from the start so events written right after rotation are not missed.
            offset = 0;
        }

        if (length < offset)
        {
            // File rotated/truncated — start from beginning of new content.
            offset = 0;
        }

        if (length == offset)
        {
            return null;
        }

        var text = ReadSlice(filePath, offset, length);
        _offsets[filePath] = length;

        return string.IsNullOrEmpty(text)
            ? null
            : new ServerLogTailSlice(filePath, text);
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
