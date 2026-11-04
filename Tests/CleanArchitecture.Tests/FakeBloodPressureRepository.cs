using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Tests;

public sealed class FakeBloodPressureRepository : IBloodPressureRepository
{
    private readonly List<BloodPressure> _items = [];
    private int _nextId = 1;

    public Task<IReadOnlyList<BloodPressure>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BloodPressure>>(_items.ToList());

    public Task<BloodPressure?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

    public Task AddAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default)
    {
        bloodPressure.Id = _nextId++;
        _items.Add(bloodPressure);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default)
    {
        _items.Remove(bloodPressure);
        return Task.CompletedTask;
    }
}
