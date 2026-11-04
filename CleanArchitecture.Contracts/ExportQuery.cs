namespace CleanArchitecture.Contracts;

/// <summary>Query-Parameter für <c>GET /api/BloodPressure/export</c>. <c>Format</c>: <c>csv</c> oder <c>pdf</c>.</summary>
public sealed record ExportQuery
{
    public const string Csv = "csv";
    public const string Pdf = "pdf";
    public const int MaxMeasurements = 10_000;

    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string Format { get; init; } = Csv;
}
