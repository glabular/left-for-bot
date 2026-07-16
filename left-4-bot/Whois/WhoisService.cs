using System.Text;
using left_4_bot.ServerLogs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace left_4_bot.Whois;

public sealed class WhoisService
{
    public const string MissingApiKeyMessage =
        "Steam API ключ не настроен. Задайте его в меню бота.";

    private readonly SteamPlayerProfileService _steamProfiles;
    private readonly IOptions<PriorityPlayersOptions> _priorityPlayers;
    private readonly ILogger<WhoisService> _logger;

    public WhoisService(
        SteamPlayerProfileService steamProfiles,
        IOptions<PriorityPlayersOptions> priorityPlayers,
        ILogger<WhoisService> logger)
    {
        _steamProfiles = steamProfiles;
        _priorityPlayers = priorityPlayers;
        _logger = logger;
    }

    public bool HasApiKey => _steamProfiles.HasApiKey;

    public IReadOnlyList<WhoisPlayerRef> FilterNonPriority(IEnumerable<WhoisPlayerRef> players)
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

    public async Task ReportPlayersAsync(
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

        var filtered = FilterNonPriority(players);
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

    private static string FormatHours(int? hours) =>
        hours is null ? "н/д" : $"{hours.Value} ч";
}
