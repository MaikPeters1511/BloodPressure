namespace CleanArchitecture.Contracts;

/// <summary>Query-Parameter für <c>GET /api/BloodPressure/statistics</c>. <c>Interval</c>: <c>day</c>, <c>week</c> (Montag) oder <c>month</c>.</summary>
public sealed record StatisticsQuery
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";

    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string Interval { get; init; } = Day;
}

public sealed record ValueStatsDto(double Average, int Min, int Max);

/// <summary>Kennzahlen eines Zeitabschnitts (UTC). <c>PeriodStart</c> ist Tag, Wochenbeginn (Montag) oder Monatsanfang.</summary>
public sealed record StatisticsBucketDto(
    DateTime PeriodStart, int Count, ValueStatsDto Systolisch, ValueStatsDto Diastolisch, ValueStatsDto Pulse);
