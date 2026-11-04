using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

public interface INotificationRepository
{
    /// <summary>Eigene Benachrichtigungen, ungelesene zuerst, dann neueste zuerst.</summary>
    Task<IReadOnlyList<Notification>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
}
