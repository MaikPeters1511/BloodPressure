namespace CleanArchitecture.Contracts;

/// <summary>Query-Parameter für <c>GET /api/BloodPressure</c>. <c>From</c>/<c>To</c> sind inklusive Grenzen (UTC).</summary>
public sealed record BloodPressureListQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
    public const string SortDateAscending = "dateTime";
    public const string SortDateDescending = "-dateTime";

    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string Sort { get; init; } = SortDateDescending;
}
