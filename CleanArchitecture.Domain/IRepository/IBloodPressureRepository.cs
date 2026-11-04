using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

/// <summary>Alle Lese-/Schreibzugriffe sind auf den Besitzer (<c>userId</c>) begrenzt.</summary>
public interface IBloodPressureRepository
{
    Task<PagedList<BloodPressure>> GetPageAsync(string userId, BloodPressureFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyAggregate>> GetDailyAggregatesAsync(string userId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<BloodPressure?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    /// <summary>Legt die Messung an; eine optionale Benachrichtigung wird in derselben Transaktion gespeichert.</summary>
    Task AddAsync(BloodPressure bloodPressure, Notification? notification = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
    Task DeleteAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
}
