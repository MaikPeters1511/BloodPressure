using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Persönliche Angaben des Nutzers; alle optional. <c>Age</c> in Jahren, <c>HeightCm</c> in cm, <c>WeightKg</c> in kg (eine Nachkommastelle).</summary>
public sealed record ProfileDto(int? Age, int? HeightCm, decimal? WeightKg);

/// <summary>Profil des angemeldeten Nutzers.</summary>
public interface IProfileApi
{
    /// <summary>Leeres Profil statt 404, wenn noch nichts gespeichert ist.</summary>
    [Get("/api/v1/profile")]
    Task<ProfileDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Ersetzt das Profil; fehlende (<c>null</c>) Angaben werden gelöscht.</summary>
    [Put("/api/v1/profile")]
    Task<ProfileDto> PutAsync([Body] ProfileDto profile, CancellationToken cancellationToken = default);

    /// <summary>Löscht alle Profilangaben.</summary>
    [Delete("/api/v1/profile")]
    Task DeleteAsync(CancellationToken cancellationToken = default);
}
