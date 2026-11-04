using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Repository;

public class NotificationRepository(AddPostgresSqlDbContext context) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
        => await context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.ReadAt != null)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Notification?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
        => await context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
