using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Tests;

public sealed class FakeBloodPressureRepository : IBloodPressureRepository
{
    private readonly List<BloodPressure> _items = [];
    private int _nextId = 1;

    public Task<IReadOnlyList<BloodPressure>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BloodPressure>>(_items.Where(i => i.UserId == userId).ToList());

    public Task<BloodPressure?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == id && i.UserId == userId));

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

    /// <summary>Direktzugriff für Prüfungen in Tests (ohne Besitzerfilter).</summary>
    public BloodPressure? Find(int id) => _items.FirstOrDefault(i => i.Id == id);
}
