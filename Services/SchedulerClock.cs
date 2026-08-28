using Microsoft.Extensions.Configuration;

namespace NotionTaskScheduler.Services;

public sealed class SchedulerClock
{
    private readonly string _timeZoneId;

    public SchedulerClock(IConfiguration configuration)
    {
        _timeZoneId = configuration["Scheduler:TimeZone"] ?? "UTC";
    }

    public DateOnly Today()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

        return DateOnly.FromDateTime(now.Date);
    }
}
