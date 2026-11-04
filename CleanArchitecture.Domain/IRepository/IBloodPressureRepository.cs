using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

/// <summary>Alle Lese-/Schreibzugriffe sind auf den Besitzer (<c>userId</c>) begrenzt.</summary>
public interface IBloodPressureRepository
{
    Task<PagedList<BloodPressure>> GetPageAsync(string userId, BloodPressureFilter filter, CancellationToken cancellationToken = default);
    Task<BloodPressure?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task AddAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
    Task UpdateAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
    Task DeleteAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
}
