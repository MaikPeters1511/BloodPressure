namespace CleanArchitecture.Domain.Entities;

/// <summary>Tägliche Erinnerung zu einer lokalen Uhrzeit in der Zeitzone des Nutzers (IANA-Kennung).</summary>
public class Reminder
{
    public const int MaxPerUser = 5;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public TimeOnly TimeOfDay { get; set; }
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Lokales Datum, für das die Erinnerung zuletzt verarbeitet wurde (verhindert Doppelungen).</summary>
    public DateOnly? LastFiredOn { get; set; }
}
