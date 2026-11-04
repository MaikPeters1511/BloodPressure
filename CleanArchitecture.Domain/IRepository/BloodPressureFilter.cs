namespace CleanArchitecture.Domain.IRepository;

/// <summary>Filter, Seite und Sortierung für Abfragen. <c>From</c>/<c>To</c> sind inklusive Grenzen in UTC.</summary>
public sealed record BloodPressureFilter(DateTime? From, DateTime? To, int Page, int PageSize, bool Descending);

/// <summary>Eine Seite von Ergebnissen samt Gesamtanzahl über alle Seiten.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount);
