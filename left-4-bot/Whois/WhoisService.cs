using System.Text;
using left_4_bot.ServerLogs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Whois;

/// <summary>
/// Builds and sends Telegram whois reports for players.
/// </summary>
public sealed class WhoisService
{
    /// <summary>
    /// Message shown when the Steam Web API key is not configured.
    /// </summary>
    public const string MissingApiKeyMessage =
        "Steam API ключ не настроен. Задайте его в меню бота.";

    private readonly SteamPlayerProfileService _steamProfiles;
    private readonly IOptions<PriorityPlayersOptions> _priorityPlayers;
    private readonly ILogger<WhoisService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhoisService"/> class.
    /// </summary>
    /// <param name="steamProfiles">Service used to load Steam profile data.</param>
    /// <param name="priorityPlayers">Priority players excluded from reports.</param>
    /// <param name="logger">Logger for this service.</param>
    public WhoisService(
        SteamPlayerProfileService steamProfiles,
        IOptions<PriorityPlayersOptions> priorityPlayers,
        ILogger<WhoisService> logger)
    {
        _steamProfiles = steamProfiles;
        _priorityPlayers = priorityPlayers;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether a Steam Web API key is configured.
    /// </summary>
    public bool HasApiKey => _steamProfiles.HasApiKey;

    /// <summary>
    /// Removes priority players and duplicates from the candidate list.
    /// </summary>
    /// <param name="players">Players to filter.</param>
    /// <returns>Filtered unique non-priority players.</returns>
    public IReadOnlyList<WhoisPlayerRef> ExcludePriorityPlayers(IEnumerable<WhoisPlayerRef> players)
    {
        var priority = _priorityPlayers.Value.SteamIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return players
            .Where(p => !priority.Contains(p.ClassicSteamId))
            .GroupBy(p => p.ClassicSteamId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Sends an HTML whois report for the given players to a Telegram chat.
    /// </summary>
    /// <param name="bot">Telegram bot client.</param>
    /// <param name="chatId">Target chat identifier.</param>
    /// <param name="players">Players to include in the report.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SendWhoisReportAsync(
        ITelegramBotClient bot,
        long chatId,
        IReadOnlyList<WhoisPlayerRef> players,
        CancellationToken cancellationToken)
    {
        if (!HasApiKey)
        {
            _logger.LogWarning("Whois Steam Web API key is not configured");
            await bot.SendMessage(
                chatId,
                MissingApiKeyMessage,
                cancellationToken: cancellationToken);

            return;
        }

        var filtered = ExcludePriorityPlayers(players);

        if (filtered.Count == 0)
        {
            await bot.SendMessage(
                chatId,
                "Нет игроков для проверки (сервер пуст или на сервере только свои).",
                cancellationToken: cancellationToken);

            return;
        }

        var profiles = await _steamProfiles.GetProfilesAsync(filtered, cancellationToken);
        var text = FormatReport(profiles);

        await bot.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Formats Steam profiles into an HTML whois report.
    /// </summary>
    /// <param name="profiles">Profiles to format.</param>
    /// <returns>HTML report text.</returns>
    internal static string FormatReport(IReadOnlyList<SteamPlayerProfile> profiles)
    {
        var builder = new StringBuilder();
        builder.AppendLine(StatReportFormatting.FormatCategoryTitle("Проверка игроков"));
        builder.AppendLine();

        foreach (var profile in profiles)
        {
            var warning = profile.VacBanCount is > 0 ? "⚠️ " : string.Empty;
            var name = StatReportFormatting.EscapeHtml(profile.Name);
            var vac = profile.VacBanCount?.ToString() ?? "н/д";

            builder.AppendLine($"• {warning}<b>{name}</b>");
            builder.AppendLine($"Left 4 Dead 2: {FormatHours(profile.L4d2Hours)}");

            if (profile.L4d1Hours is > 0)
            {
                builder.AppendLine($"Left 4 Dead: {FormatHours(profile.L4d1Hours)}");
                builder.AppendLine($"Всего: {FormatHours(profile.TotalHours)}");
            }

            builder.AppendLine($"VAC: {vac}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats playtime hours for display, or <c>н/д</c> when unknown.
    /// </summary>
    /// <param name="hours">Playtime in hours, or <see langword="null"/> if unavailable.</param>
    /// <returns>Display string for the hours value.</returns>
    private static string FormatHours(int? hours) =>
        hours is null ? "н/д" : $"{hours.Value} ч";
}
