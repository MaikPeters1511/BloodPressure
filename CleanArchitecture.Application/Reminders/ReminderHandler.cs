using System.Globalization;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;

namespace CleanArchitecture.Application.Reminders;

public sealed record GetReminders(string UserId);
public sealed record SetReminders(string UserId, ReminderSettingsDto Settings);

public static class ReminderHandler
{
    internal const string TimeFormat = "HH:mm";

    public static async Task<ReminderSettingsDto> Handle(
        GetReminders query, IReminderRepository repository, CancellationToken ct)
        => ToDto(await repository.GetForUserAsync(query.UserId, ct));

    public static async Task<ReminderSettingsDto> Handle(
        SetReminders command, IReminderRepository repository, TimeProvider timeProvider, CancellationToken ct)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(command.Settings.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);

        var reminders = command.Settings.Times
            .Select(t => TimeOnly.ParseExact(t, TimeFormat, CultureInfo.InvariantCulture))
            .OrderBy(t => t)
            .Select(time => new Reminder
            {
                UserId = command.UserId,
                TimeOfDay = time,
                TimeZoneId = command.Settings.TimeZoneId,
                // Bereits verstrichene Zeiten lösen nach dem Speichern nicht sofort aus.
                LastFiredOn = TimeOnly.FromDateTime(localNow.DateTime) >= time ? today : null
            })
            .ToList();

        await repository.ReplaceForUserAsync(command.UserId, reminders, ct);
        return ToDto(reminders);
    }

    private static ReminderSettingsDto ToDto(IReadOnlyList<Reminder> reminders)
        => new(
            reminders.Select(r => r.TimeZoneId).FirstOrDefault() ?? "UTC",
            reminders.OrderBy(r => r.TimeOfDay).Select(r => r.TimeOfDay.ToString(TimeFormat, CultureInfo.InvariantCulture)).ToList());
}
