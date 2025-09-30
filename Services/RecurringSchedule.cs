namespace SysJaky_N.Services;

public static class RecurringSchedule
{
    public static Func<DateTime, CancellationToken, ValueTask<TimeSpan>> FixedDelay(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be greater than zero.");
        }

        return (_, _) => ValueTask.FromResult(interval);
    }

    public static Func<DateTime, CancellationToken, ValueTask<TimeSpan>> DailyAtUtc(TimeSpan timeOfDay)
    {
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDay), timeOfDay, "Time of day must be within a 24-hour range.");
        }

        return (nowUtc, _) =>
        {
            var todayRun = nowUtc.Date.Add(timeOfDay);
            var nextRun = todayRun > nowUtc ? todayRun : todayRun.AddDays(1);
            return ValueTask.FromResult(nextRun - nowUtc);
        };
    }
}
