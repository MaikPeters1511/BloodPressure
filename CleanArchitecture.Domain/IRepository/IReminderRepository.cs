using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.IRepository;

public interface IReminderRepository
{
    Task<IReadOnlyList<Reminder>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Alle Erinnerungen aller Nutzer (je Nutzer höchstens <see cref="Reminder.MaxPerUser"/>) für den Scheduler.</summary>
    Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Ersetzt alle Erinnerungen des Nutzers atomar.</summary>
    Task ReplaceForUserAsync(string userId, IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default);

    Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default);
}
