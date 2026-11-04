namespace CleanArchitecture.Domain.Entities;

/// <summary>Einordnung einer Messung nach ESC/ESH 2018 (Praxiswerte). Keine Diagnose.</summary>
public enum BloodPressureCategory
{
    Optimal,
    Normal,
    HighNormal,
    Grade1,
    Grade2,
    Grade3
}

/// <summary>Ergebnis der Einordnung. <c>IsIsolatedSystolic</c>: systolisch ≥ 140 bei diastolisch &lt; 90 (Zusatzmerkmal).</summary>
public readonly record struct Classification(BloodPressureCategory Category, bool IsIsolatedSystolic);

public static class BloodPressureClassifier
{
    /// <summary>Gilt die höhere der beiden Kategorien aus Systolisch und Diastolisch.</summary>
    public static Classification Classify(int systolic, int diastolic)
    {
        var category = (BloodPressureCategory)Math.Max((int)FromSystolic(systolic), (int)FromDiastolic(diastolic));
        return new Classification(category, systolic >= 140 && diastolic < 90);
    }

    private static BloodPressureCategory FromSystolic(int value) => value switch
    {
        < 120 => BloodPressureCategory.Optimal,
        < 130 => BloodPressureCategory.Normal,
        < 140 => BloodPressureCategory.HighNormal,
        < 160 => BloodPressureCategory.Grade1,
        < 180 => BloodPressureCategory.Grade2,
        _ => BloodPressureCategory.Grade3
    };

    private static BloodPressureCategory FromDiastolic(int value) => value switch
    {
        < 80 => BloodPressureCategory.Optimal,
        < 85 => BloodPressureCategory.Normal,
        < 90 => BloodPressureCategory.HighNormal,
        < 100 => BloodPressureCategory.Grade1,
        < 110 => BloodPressureCategory.Grade2,
        _ => BloodPressureCategory.Grade3
    };
}
