using Microsoft.Extensions.Options;

namespace NotionManagementFunctionApp.TaskXp;

public sealed class DailyTargetSchedule(IOptions<TaskXpOptions> options)
{
    private readonly IReadOnlyList<(DateOnly EffectiveFrom, int TargetXp)> _items = options.Value.DailyTargets
        .Select(item => (DateOnly.ParseExact(item.EffectiveFrom, "yyyy-MM-dd"), item.DailyTargetXp)).ToArray();

    public DateOnly FirstEffectiveDate => _items[0].EffectiveFrom;

    public int TargetFor(BusinessPeriod period)
    {
        var total = 0;
        for (var date = period.Start; date < period.EndExclusive; date = date.AddDays(1)) total += TargetFor(date);
        return total;
    }

    private int TargetFor(DateOnly date)
    {
        var target = 0;
        foreach (var item in _items)
        {
            if (item.EffectiveFrom > date) break;
            target = item.TargetXp;
        }
        return target;
    }
}
