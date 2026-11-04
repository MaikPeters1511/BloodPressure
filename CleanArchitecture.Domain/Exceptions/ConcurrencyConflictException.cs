namespace CleanArchitecture.Domain.Exceptions;

/// <summary>Der Datensatz wurde zwischen Lesen und Speichern von jemand anderem geändert.</summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("Der Datensatz wurde zwischenzeitlich geändert. Bitte neu laden und erneut versuchen.", innerException);
