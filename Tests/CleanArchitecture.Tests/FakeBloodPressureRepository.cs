using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Tests;

public sealed class FakeBloodPressureRepository : IBloodPressureRepository
{
    private readonly List<BloodPressure> _items = [];
    private int _nextId = 1;

    /// <summary>Gemeinsam mit Messungen gespeicherte Benachrichtigungen.</summary>
    public List<Notification> Notifications { get; } = [];

    public Task<PagedList<BloodPressure>> GetPageAsync(string userId, BloodPressureFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _items.Where(i => i.UserId == userId
            && (filter.From is null || i.DateTime >= filter.From)
            && (filter.To is null || i.DateTime <= filter.To));
        var ordered = filter.Descending ? query.OrderByDescending(i => i.DateTime) : query.OrderBy(i => i.DateTime);
        var list = ordered.ToList();
        var page = list.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult(new PagedList<BloodPressure>(page, list.Count));
    }

    public Task<IReadOnlyList<DailyAggregate>> GetDailyAggregatesAsync(
        string userId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DailyAggregate> result = _items
            .Where(i => i.UserId == userId && (from is null || i.DateTime >= from) && (to is null || i.DateTime <= to))
            .GroupBy(i => i.DateTime.Date)
            .Select(g => new DailyAggregate(
                DateTime.SpecifyKind(g.Key, DateTimeKind.Utc), g.Count(),
                g.Sum(i => (long)i.Systolisch), g.Min(i => i.Systolisch), g.Max(i => i.Systolisch),
                g.Sum(i => (long)i.Diastolisch), g.Min(i => i.Diastolisch), g.Max(i => i.Diastolisch),
                g.Sum(i => (long)i.Pulse), g.Min(i => i.Pulse), g.Max(i => i.Pulse)))
            .OrderBy(a => a.Day)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<BloodPressure?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.FirstOrDefault(i => i.Id == id && i.UserId == userId));

    public Task AddAsync(BloodPressure bloodPressure, Notification? notification = null, CancellationToken cancellationToken = default)
    {
        if (notification is not null)
        {
            Notifications.Add(notification);
        }

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
