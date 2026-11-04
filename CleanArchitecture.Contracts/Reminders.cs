namespace CleanArchitecture.Contracts;

/// <summary>
/// Tägliche Erinnerungen eines Nutzers: bis zu 5 lokale Uhrzeiten (<c>HH:mm</c>, eindeutig) in einer IANA-Zeitzone,
/// z. B. <c>Europe/Berlin</c>.
/// </summary>
public sealed record ReminderSettingsDto(string TimeZoneId, IReadOnlyList<string> Times);

/// <summary>Hinweis an den Nutzer. <c>Type</c>: <c>CriticalReading</c> oder <c>MeasurementReminder</c>. Enthält nie Messwerte.</summary>
public sealed record NotificationDto(
    Guid Id, string Type, string? Category, string Message, DateTime CreatedAt, bool IsRead);
