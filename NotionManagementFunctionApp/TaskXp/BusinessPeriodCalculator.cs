namespace NotionManagementFunctionApp.TaskXp;

public sealed class BusinessPeriodCalculator
{
    private readonly TimeZoneInfo _warsaw;
    public BusinessPeriodCalculator()
    {
        try { _warsaw = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); }
        catch (TimeZoneNotFoundException) { _warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"); }
    }

    public DateOnly BusinessDate(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, _warsaw);
        var date = DateOnly.FromDateTime(local.DateTime);
        return local.TimeOfDay < TimeSpan.FromHours(3) ? date.AddDays(-1) : date;
    }

    public BusinessPeriod For(string period, DateOnly date) => period switch
    {
        "day" => Create("day", date, date.AddDays(1)),
        "week" => Create("week", date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)), date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)).AddDays(7)),
        "month" => Create("month", new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 1).AddMonths(1)),
        "year" => Create("year", new DateOnly(date.Year, 1, 1), new DateOnly(date.Year + 1, 1, 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    public IReadOnlyList<BusinessPeriod> ForEvent(DateTimeOffset instant)
    {
        var date = BusinessDate(instant);
        return [For("day", date), For("week", date), For("month", date), For("year", date)];
    }

    private static BusinessPeriod Create(string period, DateOnly start, DateOnly endExclusive) => new(period, start, endExclusive, $"xp-progress:{period}:{start:yyyy-MM-dd}");
}

public sealed record BusinessPeriod(string Period, DateOnly Start, DateOnly EndExclusive, string Id);
