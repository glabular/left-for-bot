using System.Text;

namespace left_4_bot.ServerLogs;

public static class FriendlyFireReportBuilder
{
    private const int TopCount = 5;

    private const int MaxVictimsPerAttackerInOthers = 3;

    private const string Subtitle = "Кто попал по своим и сколько раз.";
    private const string CleanDayText = "Логи прочитаны. Попаданий по своим за этот день нет 🎉🕊";
    private const string OthersTitle = "Остальные";

    public static IReadOnlyList<PlayStatsOutboundMessage> BuildMessages(
        FriendlyFireSectionData data,
        IReadOnlySet<string> prioritySteamIds)
    {
        if (data.State is FriendlyFireSectionState.NoLogs or FriendlyFireSectionState.DownloadFailed)
        {
            return [];
        }

        var messages = new List<PlayStatsOutboundMessage>
        {
            SilentHtml(FormatIntro()),
        };

        switch (data.State)
        {
            case FriendlyFireSectionState.CleanDay:
                messages.Add(SilentHtml(CleanDayText));
                break;

            case FriendlyFireSectionState.HasHits:
                messages.Add(SilentHtml(FormatPriorityBlock(data.Attackers, prioritySteamIds)));

                var othersText = FormatOthersBlock(data.Attackers, prioritySteamIds);
                if (othersText is not null)
                {
                    messages.Add(SilentHtml(othersText));
                }

                break;
        }

        return messages;
    }

    private static string FormatIntro()
    {
        var builder = new StringBuilder();

        builder.AppendLine(StatReportFormatting.FormatCategoryTitle(StatReportFormatting.FriendlyFireCategoryTitle));
        builder.AppendLine();
        builder.Append(Subtitle);

        return builder.ToString();
    }

    private static string FormatPriorityBlock(
        IReadOnlyDictionary<string, FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds)
    {
        var topOrdered = OrderForTop(attackers.Values, prioritySteamIds);
        var priorityAttackers = OrderAttackers(
            attackers.Values.Where(a => prioritySteamIds.Contains(a.SteamId))).ToList();

        var builder = new StringBuilder();

        builder.AppendLine("🔪 Топ за день:");
        builder.AppendLine();

        if (topOrdered.Count == 0)
        {
            builder.AppendLine("Никто из приоритетного списка за этот день не стрелял по своим");
        }
        else
        {
            var topEntries = StatReportFormatting.RankTopEntries(
                topOrdered, GetAttackerTotal, AttackerNameComparer, TopCount);

            foreach (var (placeIndex, attacker) in topEntries)
            {
                builder.AppendLine(FormatTopAttackerLine(attacker, placeIndex));
            }
        }

        builder.AppendLine();
        builder.AppendLine("Подробно:");
        builder.AppendLine();

        foreach (var attacker in priorityAttackers)
        {
            AppendAttackerDetails(builder, attacker, prioritySteamIds);
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatTopAttackerLine(FriendlyFireAttackerStats attacker, int placeIndex)
    {
        var total = GetAttackerTotal(attacker);
        var place = StatReportFormatting.FormatTopPlace(placeIndex);
        var name = StatReportFormatting.EscapeHtml(attacker.Name);

        return $"{place} {name} — {total} {PluralizeHits(total)}";
    }

    private static string? FormatOthersBlock(
        IReadOnlyDictionary<string, FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds)
    {
        var otherAttackers = OrderAttackers(
            attackers.Values.Where(a => !prioritySteamIds.Contains(a.SteamId))).ToList();

        if (otherAttackers.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        builder.AppendLine(StatReportFormatting.FormatCategoryTitle(OthersTitle));
        builder.AppendLine();

        foreach (var attacker in otherAttackers)
        {
            AppendAttackerDetails(builder, attacker, prioritySteamIds, MaxVictimsPerAttackerInOthers);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendAttackerDetails(
        StringBuilder builder,
        FriendlyFireAttackerStats attacker,
        IReadOnlySet<string> prioritySteamIds,
        int? maxVictims = null)
    {
        var total = GetAttackerTotal(attacker);
        builder.AppendLine($"{StatReportFormatting.EscapeHtml(attacker.Name)} ({total})");

        var victims = OrderVictims(attacker);
        if (maxVictims is not null)
        {
            victims = victims.Take(maxVictims.Value);
        }

        foreach (var victim in victims)
        {
            AppendVictimLine(builder, victim, prioritySteamIds);
        }

        builder.AppendLine();
    }

    private static void AppendVictimLine(
        StringBuilder builder,
        FriendlyFireVictimStats victim,
        IReadOnlySet<string> prioritySteamIds)
    {
        var line = $"{StatReportFormatting.EscapeHtml(victim.Name)}: {victim.HitCount}";

        if (prioritySteamIds.Contains(victim.SteamId))
        {
            line = $"<b>{line}</b>";
        }

        builder.AppendLine($"  → {line}");
    }

    private static List<FriendlyFireAttackerStats> OrderForTop(
        IEnumerable<FriendlyFireAttackerStats> attackers,
        IReadOnlySet<string> prioritySteamIds) =>
        OrderAttackers(attackers.Where(a => prioritySteamIds.Contains(a.SteamId))).ToList();

    private static int GetAttackerTotal(FriendlyFireAttackerStats attacker) =>
        attacker.Victims.Values.Sum(v => v.HitCount);

    private static string PluralizeHits(int count)
    {
        var mod10 = count % 10;
        var mod100 = count % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "попадание";
        }

        if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14))
        {
            return "попадания";
        }

        return "попаданий";
    }

    private static readonly IComparer<FriendlyFireAttackerStats> AttackerNameComparer =
        Comparer<FriendlyFireAttackerStats>.Create(
            (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<FriendlyFireAttackerStats> OrderAttackers(
        IEnumerable<FriendlyFireAttackerStats> attackers) =>
        attackers
            .OrderByDescending(GetAttackerTotal)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<FriendlyFireVictimStats> OrderVictims(FriendlyFireAttackerStats attacker) =>
        attacker.Victims.Values
            .OrderByDescending(v => v.HitCount)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase);

    private static PlayStatsOutboundMessage SilentHtml(string text) =>
        new()
        {
            Text = text,
            UseHtml = true,
            DisableNotification = true,
        };
}
