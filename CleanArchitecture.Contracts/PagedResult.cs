namespace CleanArchitecture.Contracts;

/// <summary>Eine Seite einer Ergebnisliste samt Gesamtanzahl.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
