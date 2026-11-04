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
        set => _systolisch = EnsureInRange(value, 0, 500, nameof(Systolisch));
    }

    public int Diastolisch
    {
        get => _diastolisch;
        set => _diastolisch = EnsureInRange(value, 0, 500, nameof(Diastolisch));
    }

    public DateTime DateTime { get; set; } = DateTime.UtcNow;

    public int Pulse
    {
        get => _pulse;
        set => _pulse = EnsureInRange(value, 0, 500, nameof(Pulse));
    }

    public int Oxygen
    {
        get => _oxygen;
        set => _oxygen = EnsureInRange(value, 0, 100, nameof(Oxygen));
    }

    private static int EnsureInRange(int value, int min, int max, string name)
        => value >= min && value <= max
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"{name} muss zwischen {min} und {max} liegen.");
}
