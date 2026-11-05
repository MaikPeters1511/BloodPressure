namespace CleanArchitecture.Contracts;

/// <summary>Berechnungen aus den Profilangaben. Reine Anzeigewerte, keine Bewertung.</summary>
public static class ProfileCalculator
{
    /// <summary>Body-Mass-Index (kg / m²), kaufmännisch auf eine Nachkommastelle gerundet; <c>null</c>, wenn eine Angabe fehlt.</summary>
    public static decimal? Bmi(int? heightCm, decimal? weightKg)
    {
        if (heightCm is null or <= 0 || weightKg is null)
        {
            return null;
        }

        var meters = heightCm.Value / 100m;
        return decimal.Round(weightKg.Value / (meters * meters), 1, MidpointRounding.AwayFromZero);
    }
}
