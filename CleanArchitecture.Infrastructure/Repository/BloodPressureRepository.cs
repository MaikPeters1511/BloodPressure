using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class BloodPressureRepository(AddPostgresSqlDbContext context) : IBloodPressureRepository
{
    public async Task<IReadOnlyList<BloodPressure>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.BloodPressures.AsNoTracking().OrderBy(b => b.DateTime).ToListAsync(cancellationToken);

    public async Task<BloodPressure?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.BloodPressures.FindAsync([id], cancellationToken);

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
