using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class BloodPressureRepository : IBloodPressureRepository
{
    private readonly AddPostgresSqlDbContext _context;

    public BloodPressureRepository(AddPostgresSqlDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BloodPressure>> GetAllAsync()
    {
        return await _context.BloodPressures.ToListAsync();
    }

    public async Task<BloodPressure> GetByIdAsync(int id)
    {
        return await _context.BloodPressures.FindAsync(id);
    }

    public async Task AddAsync(BloodPressure bloodPressure)
    {
        _context.BloodPressures.Add(bloodPressure);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(BloodPressure bloodPressure)
    {
        _context.Entry(bloodPressure).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(BloodPressure bloodPressure)
    {
        _context.BloodPressures.Remove(bloodPressure);
        await _context.SaveChangesAsync();
    }
}