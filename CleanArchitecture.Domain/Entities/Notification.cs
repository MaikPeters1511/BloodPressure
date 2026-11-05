namespace CleanArchitecture.Domain.Entities;

public enum NotificationType
{
    CriticalReading,
    MeasurementReminder
}

/// <summary>
/// Hinweis an einen Nutzer. Der Text enthält nie Messwerte (Vorschau auf dem Sperrbildschirm), bei kritischen
/// Werten nur die Kategorie, und stellt keine Diagnose.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public NotificationType Type { get; set; }
    public string? Category { get; set; }
    public int? BloodPressureId { get; set; }
    public BloodPressure? BloodPressure { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    /// <summary>Hinweis, wenn eine Messung stark erhöht ist (ab 180/110 mmHg); sonst <c>null</c>.</summary>
    public static Notification? ForCriticalReading(BloodPressure measurement, DateTime nowUtc)
    {
        var classification = BloodPressureClassifier.Classify(measurement.Systolisch, measurement.Diastolisch);
        if (!BloodPressureClassifier.IsSevere(measurement.Systolisch, measurement.Diastolisch) || measurement.UserId is null)
        {
            return null;
        }

        return new Notification
        {
            UserId = measurement.UserId,
            Type = NotificationType.CriticalReading,
            Category = classification.Category.ToString(),
            BloodPressure = measurement,
            Message = "Eine Messung liegt im Bereich eines stark erhöhten Blutdrucks (Kategorie Bluthochdruck). Das ist keine Diagnose. " +
                      "Bitte bei Beschwerden oder anhaltend hohen Werten ärztlichen Rat einholen.",
            CreatedAt = nowUtc
        };
    }

    public static Notification ForReminder(string userId, DateTime nowUtc) => new()
    {
        UserId = userId,
        Type = NotificationType.MeasurementReminder,
        Message = "Zeit für deine Blutdruckmessung.",
        CreatedAt = nowUtc
    };
}
