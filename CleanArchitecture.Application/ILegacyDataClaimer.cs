namespace CleanArchitecture.Application;

/// <summary>
/// Weist Altdaten ohne Besitzer (aus der Zeit vor der Anmeldung) dem ersten registrierten Nutzer zu.
/// Wird unmittelbar nach erfolgreicher Registrierung aufgerufen.
/// </summary>
public interface ILegacyDataClaimer
{
    Task ClaimForFirstUserAsync(string userId, CancellationToken cancellationToken = default);
}
