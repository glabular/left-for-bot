namespace left_4_bot.ServerLogs;

internal static class GamingDay
{
    private const int MoscowUtcOffsetHours = 3;

    public static DateTime NowMoscow() =>
        DateTime.UtcNow.AddHours(MoscowUtcOffsetHours);

    public static DateOnly GetCurrentLabel(GamingDayOptions options) =>
        GetLabelFor(NowMoscow(), options);

    public static DateOnly GetLabelFor(DateTime moscowTime, GamingDayOptions options)
    {
        var calendarDate = DateOnly.FromDateTime(moscowTime);
        var dayStartToday = ToDayStart(calendarDate, options);

        return moscowTime >= dayStartToday
            ? calendarDate
            : calendarDate.AddDays(-1);
    }

    public static GamingDayBounds GetBounds(DateOnly label, GamingDayOptions options)
    {
        var start = ToDayStart(label, options);
        return new GamingDayBounds(start, start.AddDays(1));
    }

    public static IReadOnlyList<DateOnly> GetCalendarDatesToDownload(GamingDayBounds bounds)
    {
        var dates = new List<DateOnly>();
        var date = DateOnly.FromDateTime(bounds.StartInclusive);
        var lastDate = DateOnly.FromDateTime(bounds.EndExclusive.AddTicks(-1));

        while (date <= lastDate)
        {
            dates.Add(date);
            date = date.AddDays(1);
        }

        return dates;
    }

    public static bool Contains(GamingDayBounds bounds, DateTime eventTime) =>
        eventTime >= bounds.StartInclusive && eventTime < bounds.EndExclusive;

    private static DateTime ToDayStart(DateOnly label, GamingDayOptions options) =>
        label.ToDateTime(new TimeOnly(options.GamingDayStartHour, 0));
}
