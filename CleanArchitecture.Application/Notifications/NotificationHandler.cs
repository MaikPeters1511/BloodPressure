using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.Notifications;

public sealed record GetNotifications(string UserId);
public sealed record MarkNotificationRead(string UserId, Guid Id);

public static class NotificationHandler
{
    public static async Task<IReadOnlyList<NotificationDto>> Handle(
        GetNotifications query, INotificationRepository repository, CancellationToken ct)
        => (await repository.GetForUserAsync(query.UserId, ct)).Select(ToDto).ToList();

    /// <summary>Gibt <c>false</c> zurück, wenn es die Benachrichtigung nicht (für diesen Nutzer) gibt.</summary>
    public static async Task<bool> Handle(
        MarkNotificationRead command, INotificationRepository repository, TimeProvider timeProvider, CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(command.Id, command.UserId, ct);
        if (notification is null)
        {
            return false;
        }

        notification.ReadAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(notification, ct);
        return true;
    }

    private static NotificationDto ToDto(Notification n)
        => new(n.Id, n.Type.ToString(), n.Category, n.Message, n.CreatedAt, n.ReadAt is not null);
}
