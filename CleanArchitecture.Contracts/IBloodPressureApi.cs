using Refit;

namespace CleanArchitecture.Contracts;

/// <summary>Typisierter Client für <c>/api/BloodPressure</c>. Fehlerantworten (400/404) werden als <see cref="ApiException"/> geworfen.</summary>
public interface IBloodPressureApi
{
    /// <summary>Paginierte Liste; <paramref name="from"/>/<paramref name="to"/> inklusive (UTC), Standard-Sortierung neueste zuerst.</summary>
    [Get("/api/BloodPressure")]
    Task<PagedResult<BloodPressureDto>> GetAllAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    /// <summary>Durchschnitt, Minimum, Maximum und Anzahl je Tag, Woche (Montag) oder Monat; Abschnitte ohne Messung fehlen.</summary>
    [Get("/api/BloodPressure/statistics")]
    Task<IReadOnlyList<StatisticsBucketDto>> GetStatisticsAsync(
        [Query(Format = "o")] DateTime? from = null,
        [Query(Format = "o")] DateTime? to = null,
        string? interval = null,
        CancellationToken cancellationToken = default);

    [Get("/api/BloodPressure/{id}")]
    Task<BloodPressureDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    [Post("/api/BloodPressure")]
    Task<BloodPressureDto> CreateAsync([Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Put("/api/BloodPressure/{id}")]
    Task UpdateAsync(int id, [Body] BloodPressureRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/BloodPressure/{id}")]
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
