using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class BloodPressureRepository(AddPostgresSqlDbContext context) : IBloodPressureRepository
{
    public async Task<PagedList<BloodPressure>> GetPageAsync(
        string userId, BloodPressureFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.BloodPressures.AsNoTracking().Where(b => b.UserId == userId);
        if (filter.From is { } from)
        {
            query = query.Where(b => b.DateTime >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(b => b.DateTime <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = filter.Descending ? query.OrderByDescending(b => b.DateTime) : query.OrderBy(b => b.DateTime);
        var items = await ordered.ThenBy(b => b.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedList<BloodPressure>(items, total);
    }

    public async Task<BloodPressure?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
        => await context.BloodPressures.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, cancellationToken);

    public async Task AddAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default)
    {
        context.BloodPressures.Add(bloodPressure);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default)
    {
        // Entität wurde über denselben (scoped) Context geladen und ist bereits getrackt.
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BloodPressure bloodPressure, CancellationToken cancellationToken = default)
    {
        context.BloodPressures.Remove(bloodPressure);
        await context.SaveChangesAsync(cancellationToken);
    }
}
