namespace CleanArchitecture.Domain.Entities;

/// <summary>
/// Eine einzelne Blutdruckmessung. Invarianten werden in den Settern erzwungen,
/// die Domain bleibt frei von Infrastruktur-Abhängigkeiten.
/// </summary>
public class BloodPressure
{
    private int _systolisch;
    private int _diastolisch;
    private int _pulse;
    private int _oxygen;

    public int Id { get; set; }

    /// <summary>Besitzer der Messung (Identity-Benutzer-Id). <c>null</c> = Altdaten ohne Besitzer.</summary>
    public string? UserId { get; set; }

    public int Systolisch
    {
        get => _systolisch;
        set => _systolisch = EnsureInRange(value, BloodPressureLimits.PressureMin, BloodPressureLimits.PressureMax, nameof(Systolisch));
    }

    public int Diastolisch
    {
        get => _diastolisch;
        set => _diastolisch = EnsureInRange(value, BloodPressureLimits.PressureMin, BloodPressureLimits.PressureMax, nameof(Diastolisch));
    }

    public DateTime DateTime { get; set; } = DateTime.UtcNow;

    /// <summary>Zeilenversion für Optimistic Concurrency (PostgreSQL <c>xmin</c>); wird von der Datenbank vergeben.</summary>
    public uint Version { get; set; }

    public int Pulse
    {
        get => _pulse;
        set => _pulse = EnsureInRange(value, BloodPressureLimits.PulseMin, BloodPressureLimits.PulseMax, nameof(Pulse));
    }

    public int Oxygen
    {
        get => _oxygen;
        set => _oxygen = EnsureInRange(value, BloodPressureLimits.OxygenMin, BloodPressureLimits.OxygenMax, nameof(Oxygen));
    }

    private static int EnsureInRange(int value, int min, int max, string name)
        => value >= min && value <= max
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"{name} muss zwischen {min} und {max} liegen.");
}
