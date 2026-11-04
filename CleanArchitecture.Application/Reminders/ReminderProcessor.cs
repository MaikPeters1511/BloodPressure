using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Application.Reminders;

/// <summary>
/// Verarbeitet fällige Erinnerungen. Eine Erinnerung zur Uhrzeit T erzeugt eine Benachrichtigung, außer der Nutzer hat
/// in den <see cref="SuppressWindow"/> vor T bereits gemessen. Wurde T um mehr als <see cref="Grace"/> verpasst
/// (z. B. Ausfall des Dienstes), wird die Erinnerung für den Tag ohne Benachrichtigung abgeschlossen.
/// </summary>
public sealed class ReminderProcessor(
    IReminderRepository reminders,
    IBloodPressureRepository measurements,
    INotificationRepository notifications,
    TimeProvider timeProvider,
    ILogger<ReminderProcessor> logger)
{
    public static readonly TimeSpan SuppressWindow = TimeSpan.FromHours(2);
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(30);

    public async Task ProcessDueAsync(CancellationToken ct)
    {
        var nowUtc = timeProvider.GetUtcNow();

        foreach (var reminder in await reminders.GetAllAsync(ct))
        {
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(reminder.TimeZoneId);
            }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogWarning(ex, "Unbekannte Zeitzone {TimeZone} bei Erinnerung {ReminderId}", reminder.TimeZoneId, reminder.Id);
                continue;
            }

            var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone).DateTime;
            var today = DateOnly.FromDateTime(localNow);
            var dueLocal = today.ToDateTime(reminder.TimeOfDay);
            if (timeZone.IsInvalidTime(dueLocal))
            {
                dueLocal = dueLocal.AddHours(1); // Zeit fällt in die Sommerzeit-Lücke
            }

            if (reminder.LastFiredOn == today || localNow < dueLocal)
            {
                continue;
            }

            reminder.LastFiredOn = today;
            await reminders.UpdateAsync(reminder, ct);

            if (localNow - dueLocal > Grace)
            {
                continue;
            }

            var dueUtc = TimeZoneInfo.ConvertTimeToUtc(dueLocal, timeZone);
            var recent = await measurements.GetPageAsync(
                reminder.UserId, new BloodPressureFilter(dueUtc - SuppressWindow, dueUtc, 1, 1, Descending: true), ct);
            if (recent.TotalCount == 0)
            {
                await notifications.AddAsync(Notification.ForReminder(reminder.UserId, nowUtc.UtcDateTime), ct);
            }
        }
    }
}
