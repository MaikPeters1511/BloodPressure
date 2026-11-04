using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.BloodPressures;

public static class StatisticsHandler
{
    public static async Task<IReadOnlyList<StatisticsBucketDto>> Handle(
        GetStatistics query, IBloodPressureRepository repository, CancellationToken ct)
    {
        var days = await repository.GetDailyAggregatesAsync(
            query.UserId, BloodPressureHandler.ToUtc(query.Query.From), BloodPressureHandler.ToUtc(query.Query.To), ct);
        return Rollup(days, query.Query.Interval);
    }

    /// <summary>Fasst Tagesaggregate zu Wochen (Beginn Montag) bzw. Monaten zusammen; Mittelwerte aus Summen und Anzahl.</summary>
    public static IReadOnlyList<StatisticsBucketDto> Rollup(IEnumerable<DailyAggregate> days, string interval)
    {
        return days
            .GroupBy(d => PeriodStart(d.Day, interval))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var count = g.Sum(d => d.Count);
                return new StatisticsBucketDto(
                    g.Key,
                    count,
                    Stats(g.Sum(d => d.SumSystolisch), count, g.Min(d => d.MinSystolisch), g.Max(d => d.MaxSystolisch)),
                    Stats(g.Sum(d => d.SumDiastolisch), count, g.Min(d => d.MinDiastolisch), g.Max(d => d.MaxDiastolisch)),
                    Stats(g.Sum(d => d.SumPulse), count, g.Min(d => d.MinPulse), g.Max(d => d.MaxPulse)));
            })
            .ToList();
    }

    private static ValueStatsDto Stats(long sum, int count, int min, int max) => new((double)sum / count, min, max);

    private static DateTime PeriodStart(DateTime day, string interval)
    {
        var utcDay = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        if (string.Equals(interval, StatisticsQuery.Week, StringComparison.OrdinalIgnoreCase))
        {
            return utcDay.AddDays(-(((int)utcDay.DayOfWeek + 6) % 7));
        }

        return string.Equals(interval, StatisticsQuery.Month, StringComparison.OrdinalIgnoreCase)
            ? new DateTime(utcDay.Year, utcDay.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : utcDay;
    }
}
