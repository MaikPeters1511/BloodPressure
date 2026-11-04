namespace CleanArchitecture.Domain.Entities;

/// <summary>Gültige Wertebereiche einer Messung. Einzige Quelle für Domain-Invarianten und Request-Validierung.</summary>
public static class BloodPressureLimits
{
    public const int PressureMin = 0;
    public const int PressureMax = 500;
    public const int PulseMin = 0;
    public const int PulseMax = 500;
    public const int OxygenMin = 0;
    public const int OxygenMax = 100;

    /// <summary>Toleranz für Uhrenabweichungen zwischen Gerät und Server.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);
}
