using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class ReminderRepository(AddPostgresSqlDbContext context) : IReminderRepository
{
    public async Task<IReadOnlyList<Reminder>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
        => await context.Reminders.AsNoTracking().Where(r => r.UserId == userId).OrderBy(r => r.TimeOfDay).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Reminders.ToListAsync(cancellationToken);

    public async Task ReplaceForUserAsync(string userId, IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
    {
        // Löschen und Einfügen in einer Transaktion (ein SaveChanges).
        context.Reminders.RemoveRange(await context.Reminders.Where(r => r.UserId == userId).ToListAsync(cancellationToken));
        context.Reminders.AddRange(reminders);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
