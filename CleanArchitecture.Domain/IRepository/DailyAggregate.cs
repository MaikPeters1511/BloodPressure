namespace CleanArchitecture.Domain.IRepository;

/// <summary>Tagesaggregat (UTC-Tag) für Statistiken; Summen statt Mittelwerte, damit sich Tage korrekt zusammenfassen lassen.</summary>
public sealed record DailyAggregate(
    DateTime Day,
    int Count,
    long SumSystolisch, int MinSystolisch, int MaxSystolisch,
    long SumDiastolisch, int MinDiastolisch, int MaxDiastolisch,
    long SumPulse, int MinPulse, int MaxPulse);
