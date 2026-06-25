using System.Net;

namespace left_4_bot.ServerLogs;

public static class StatReportFormatting
{
    public const string FriendlyFireCategoryTitle = "Дружественный огонь";

    public static readonly string[] TopPlaceEmojis =
    [
        "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣",
        "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟",
    ];

    public static string FormatCategoryTitle(string title) =>
        $"<b><u>{EscapeHtml(title)}</u></b>";

    public static string EscapeHtml(string text) => WebUtility.HtmlEncode(text);
}
