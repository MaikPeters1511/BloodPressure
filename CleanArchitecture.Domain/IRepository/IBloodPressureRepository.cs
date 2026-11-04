using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

public interface IBloodPressureRepository
{
    Task<IReadOnlyList<BloodPressure>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BloodPressure?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
    Task UpdateAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
    Task DeleteAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default);
}
