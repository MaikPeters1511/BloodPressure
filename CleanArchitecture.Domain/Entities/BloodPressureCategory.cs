namespace CleanArchitecture.Domain.Entities;

/// <summary>
/// Einordnung einer Messung nach den ESC-Leitlinien 2024 (Praxiswerte): nicht erhöht (unter 120/70 mmHg), erhöht
/// (120–139 und/oder 70–89 mmHg) und Bluthochdruck (ab 140 und/oder 90 mmHg). Die Leitlinie kennt keine Schweregrade
/// 1–3 mehr. Keine Diagnose: Die Diagnose erfordert wiederholte Messungen und ärztliche Beurteilung.
/// </summary>
public enum BloodPressureCategory
{
    NonElevated,
    Elevated,
    Hypertension
}

/// <summary>Ergebnis der Einordnung. <c>IsIsolatedSystolic</c>: systolisch ≥ 140 bei diastolisch &lt; 90 (Zusatzmerkmal).</summary>
public readonly record struct Classification(BloodPressureCategory Category, bool IsIsolatedSystolic);

public static class BloodPressureClassifier
{
    /// <summary>Ab diesen Werten gilt eine Messung als stark erhöht (Anlass für einen Hinweis): 180 bzw. 110 mmHg.</summary>
    public const int SevereSystolic = 180;

    public const int SevereDiastolic = 110;

    /// <summary>Gilt die höhere der beiden Kategorien aus Systolisch und Diastolisch.</summary>
    public static Classification Classify(int systolic, int diastolic)
    {
        var category = (BloodPressureCategory)Math.Max((int)FromSystolic(systolic), (int)FromDiastolic(diastolic));
        return new Classification(category, systolic >= 140 && diastolic < 90);
    }

    /// <summary>Stark erhöhter Wert (≥ 180 systolisch oder ≥ 110 diastolisch); unabhängig von der Kategorie.</summary>
    public static bool IsSevere(int systolic, int diastolic) => systolic >= SevereSystolic || diastolic >= SevereDiastolic;

    private static BloodPressureCategory FromSystolic(int value) => value switch
    {
        < 120 => BloodPressureCategory.NonElevated,
        < 140 => BloodPressureCategory.Elevated,
        _ => BloodPressureCategory.Hypertension
    };

    private static BloodPressureCategory FromDiastolic(int value) => value switch
    {
        < 70 => BloodPressureCategory.NonElevated,
        < 90 => BloodPressureCategory.Elevated,
        _ => BloodPressureCategory.Hypertension
    };
}
