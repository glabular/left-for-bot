namespace left_4_bot.ServerLogs;

/// <summary>
/// Gaming-day calendar for Moscow time (UTC+3, no DST).
/// A gaming day is a 24-hour window starting at <see cref="GamingDayOptions.GamingDayStartHour"/>,
/// not at midnight. Its <c>anchor</c> is the <see cref="DateOnly"/> on which that window begins.
/// </summary>
internal static class GamingDay
{
    private const int MoscowUtcOffsetHours = 3;

    /// <summary>
    /// Gets the current date and time in Moscow (UTC+3).
    /// </summary>
    /// <returns>The current Moscow wall-clock time.</returns>
    public static DateTime NowMoscow() =>
        DateTime.UtcNow.AddHours(MoscowUtcOffsetHours);

    /// <summary>
    /// Gets the anchor of the gaming day that is currently in progress.
    /// </summary>
    /// <param name="options">Gaming-day configuration, including the start hour.</param>
    /// <returns>
    /// The calendar date that identifies the current gaming day.
    /// Before <see cref="GamingDayOptions.GamingDayStartHour"/> on a calendar date,
    /// the anchor is the previous calendar date.
    /// </returns>
    public static DateOnly GetCurrentAnchor(GamingDayOptions options) =>
        GetAnchorFor(NowMoscow(), options);

    /// <summary>
    /// Gets the gaming-day anchor for a specific Moscow timestamp.
    /// </summary>
    /// <param name="moscowTime">A timestamp in Moscow time (UTC+3).</param>
    /// <param name="options">Gaming-day configuration, including the start hour.</param>
    /// <returns>
    /// The calendar date that identifies the gaming day containing <paramref name="moscowTime"/>.
    /// </returns>
    public static DateOnly GetAnchorFor(DateTime moscowTime, GamingDayOptions options)
    {
        var calendarDate = DateOnly.FromDateTime(moscowTime);
        var dayStartToday = ToDayStart(calendarDate, options);

        return moscowTime >= dayStartToday
            ? calendarDate
            : calendarDate.AddDays(-1);
    }

    /// <summary>
    /// Gets the inclusive time range for a gaming day identified by <paramref name="anchor"/>.
    /// </summary>
    /// <param name="anchor">The gaming-day anchor (the calendar date when the window starts).</param>
    /// <param name="options">Gaming-day configuration, including the start hour.</param>
    /// <returns>
    /// A half-open interval from the start hour on <paramref name="anchor"/>
    /// to the same hour on the next calendar date.
    /// </returns>
    public static GamingDayBounds GetBounds(DateOnly anchor, GamingDayOptions options)
    {
        var start = ToDayStart(anchor, options);

        return new GamingDayBounds(start, start.AddDays(1));
    }

    /// <summary>
    /// Lists calendar dates whose log files may contain events within a gaming-day range.
    /// </summary>
    /// <param name="bounds">The gaming-day time range.</param>
    /// <returns>
    /// One or two calendar dates, because a gaming day can span two calendar midnights.
    /// </returns>
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

    /// <summary>
    /// Determines whether a timestamp falls within a gaming-day range.
    /// </summary>
    /// <param name="bounds">The gaming-day time range.</param>
    /// <param name="eventTime">The timestamp to test.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="eventTime"/> is greater than or equal to
    /// <see cref="GamingDayBounds.StartInclusive"/> and less than
    /// <see cref="GamingDayBounds.EndExclusive"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Contains(GamingDayBounds bounds, DateTime eventTime) =>
        eventTime >= bounds.StartInclusive && eventTime < bounds.EndExclusive;

    /// <summary>
    /// Converts a gaming-day anchor to the Moscow timestamp when that day starts.
    /// </summary>
    /// <param name="anchor">The gaming-day anchor.</param>
    /// <param name="options">Gaming-day configuration, including the start hour.</param>
    /// <returns>The start of the gaming day in Moscow time.</returns>
    private static DateTime ToDayStart(DateOnly anchor, GamingDayOptions options) =>
        anchor.ToDateTime(new TimeOnly(options.GamingDayStartHour, 0));
}
