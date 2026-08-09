using System.Globalization;
using System.Text;
using left_4_bot.ServerLogs;
using left_4_bot.Whois;

namespace left_4_bot.Rcon;

/// <summary>
/// Formats numbered moderation lists for Telegram HTML.
/// </summary>
public static class ModerationListFormatter
{
    private const int TelegramMessageLimit = 4096;
    private const string TruncationSuffix = "\n\n… (обрезано)";

    /// <summary>
    /// Builds an HTML list of recent players for <c>/ban</c>.
    /// </summary>
    public static string FormatBanCandidates(IReadOnlyList<RecentPlayerEntry> players)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<b>Игроки за последние 24 часа</b>");
        builder.AppendLine();

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            builder.Append((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(". ");
            builder.Append(FormatClickableName(player.Name, player.ClassicSteamId));
            builder.AppendLine();
        }

        builder.AppendLine();
        builder.Append("Отправьте <code>/ban N</code> или <code>/ban &lt;Steam&gt;</code>, чтобы навсегда забанить игрока на сервере.");

        return TruncateForTelegram(builder.ToString().TrimEnd());
    }

    /// <summary>
    /// Builds an HTML list of banned players for <c>/unban</c>.
    /// </summary>
    public static string FormatUnbanEntries(IReadOnlyList<UnbanSelectionEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<b>Забаненные на сервере</b>");
        builder.AppendLine();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            builder.Append((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(". ");
            builder.Append(FormatClickableName(entry.DisplayName, entry.ClassicSteamId));

            if (!string.IsNullOrWhiteSpace(entry.DurationLabel))
            {
                builder.Append(" — ");
                builder.Append(StatReportFormatting.EscapeHtml(entry.DurationLabel));
            }

            builder.AppendLine();
        }

        builder.AppendLine();
        builder.Append("Отправьте <code>/unban N</code> или <code>/unban &lt;Steam&gt;</code>, чтобы снять бан.");

        return TruncateForTelegram(builder.ToString().TrimEnd());
    }

    /// <summary>
    /// Formats a player name as an HTML Steam profile link when Steam64 is available.
    /// </summary>
    public static string FormatClickableName(string name, string classicSteamId)
    {
        var escapedName = StatReportFormatting.EscapeHtml(name);

        if (SteamIdConverter.TryToSteam64(classicSteamId, out var steam64))
        {
            return
                $"<a href=\"https://steamcommunity.com/profiles/{steam64.ToString(CultureInfo.InvariantCulture)}\">{escapedName}</a>";
        }

        return escapedName;
    }

    /// <summary>
    /// Builds the «Баню …» progress line (HTML, clickable name only).
    /// </summary>
    public static string FormatBanProgress(string name, string classicSteamId) =>
        $"Баню {FormatClickableName(name, classicSteamId)}";

    /// <summary>
    /// Builds the successful ban confirmation (HTML, clickable name only).
    /// </summary>
    public static string FormatBanSuccess(string name, string classicSteamId, string? rconDetail = null)
    {
        var text = $"Игрок {FormatClickableName(name, classicSteamId)} забанен навсегда.";

        if (!string.IsNullOrWhiteSpace(rconDetail))
        {
            text += "\n" + StatReportFormatting.EscapeHtml(rconDetail);
        }

        return text;
    }

    /// <summary>
    /// Builds the «Снимаю бан …» progress line (HTML, clickable name).
    /// </summary>
    public static string FormatUnbanProgress(string name, string classicSteamId) =>
        $"Снимаю бан {FormatClickableName(name, classicSteamId)}";

    /// <summary>
    /// Builds the successful unban confirmation (HTML, clickable name).
    /// </summary>
    public static string FormatUnbanSuccess(string name, string classicSteamId) =>
        $"Бан снят с {FormatClickableName(name, classicSteamId)}";


    private static string TruncateForTelegram(string text)
    {
        if (text.Length <= TelegramMessageLimit)
        {
            return text;
        }

        var maxLength = TelegramMessageLimit - TruncationSuffix.Length;
        return text[..maxLength] + TruncationSuffix;
    }
}
