namespace CleanArchitecture.Application.BloodPressures;

/// <summary>Die mit <c>If-Match</c> übergebene Version entspricht nicht mehr dem aktuellen Stand (HTTP 412).</summary>
public sealed class PreconditionFailedException()
    : Exception("Der Datensatz wurde zwischenzeitlich geändert (If-Match stimmt nicht mehr). Bitte neu laden.");
