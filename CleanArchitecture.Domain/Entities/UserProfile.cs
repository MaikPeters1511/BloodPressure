namespace CleanArchitecture.Domain.Entities;

/// <summary>Grenzen der Profilangaben.</summary>
public static class ProfileLimits
{
    public const int MinAge = 0;
    public const int MaxAge = 120;
    public const int MinHeightCm = 50;
    public const int MaxHeightCm = 250;
    public const decimal MinWeightKg = 2m;
    public const decimal MaxWeightKg = 500m;
}

/// <summary>
/// Persönliche Angaben eines Nutzers (Gesundheitsdaten, besonders schützenswert). Statt des Alters wird das Geburtsjahr
/// gespeichert, damit das Alter über die Jahre richtig bleibt. Alle Angaben sind optional.
/// </summary>
public class UserProfile
{
    public string UserId { get; set; } = "";
    public int? BirthYear { get; private set; }
    public int? HeightCm { get; private set; }
    public decimal? WeightKg { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool IsEmpty => BirthYear is null && HeightCm is null && WeightKg is null;

    /// <summary>Ersetzt alle Angaben. <c>null</c> löscht die jeweilige Angabe. Prüft die Grenzen.</summary>
    public void Apply(int? age, int? heightCm, decimal? weightKg, DateTime nowUtc)
    {
        if (age is { } a && a is < ProfileLimits.MinAge or > ProfileLimits.MaxAge)
        {
            throw new ArgumentOutOfRangeException(nameof(age), a, $"Das Alter muss zwischen {ProfileLimits.MinAge} und {ProfileLimits.MaxAge} liegen.");
        }

        if (heightCm is { } h && h is < ProfileLimits.MinHeightCm or > ProfileLimits.MaxHeightCm)
        {
            throw new ArgumentOutOfRangeException(nameof(heightCm), h, $"Die Größe muss zwischen {ProfileLimits.MinHeightCm} und {ProfileLimits.MaxHeightCm} cm liegen.");
        }

        if (weightKg is { } w && w < ProfileLimits.MinWeightKg || weightKg is { } w2 && w2 > ProfileLimits.MaxWeightKg)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg), weightKg, $"Das Gewicht muss zwischen {ProfileLimits.MinWeightKg} und {ProfileLimits.MaxWeightKg} kg liegen.");
        }

        BirthYear = age is { } years ? nowUtc.Year - years : null;
        HeightCm = heightCm;
        WeightKg = weightKg;
        UpdatedAt = nowUtc;
    }

    /// <summary>Alter in ganzen Jahren zum angegebenen Zeitpunkt (Geburtstag unbekannt: höchstens ein Jahr Abweichung).</summary>
    public int? AgeAt(DateTime nowUtc) => BirthYear is { } year ? nowUtc.Year - year : null;
}
