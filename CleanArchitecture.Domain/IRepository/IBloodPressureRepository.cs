using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

public interface IBloodPressureRepository
{
    Task<IEnumerable<BloodPressure>> GetAllAsync();
    Task<BloodPressure> GetByIdAsync(int id);
    Task AddAsync(BloodPressure bloodPressure);
    Task UpdateAsync(BloodPressure bloodPressure);
    Task DeleteAsync(BloodPressure bloodPressure);
}