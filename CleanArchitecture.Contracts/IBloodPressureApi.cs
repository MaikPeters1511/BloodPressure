using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Typisierter Client für <c>/api/BloodPressure</c>. Fehlerantworten (400/404) werden als <see cref="ApiException"/> geworfen.</summary>
public interface IBloodPressureApi
{
    [Get("/api/BloodPressure")]
    Task<IReadOnlyList<BloodPressureDto>> GetAllAsync(CancellationToken cancellationToken = default);

    [Get("/api/BloodPressure/{id}")]
    Task<BloodPressureDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    [Post("/api/BloodPressure")]
    Task<BloodPressureDto> CreateAsync([Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Put("/api/BloodPressure/{id}")]
    Task UpdateAsync(int id, [Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/BloodPressure/{id}")]
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
