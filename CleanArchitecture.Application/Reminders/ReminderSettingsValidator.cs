using System.Globalization;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using FluentValidation;

namespace CleanArchitecture.Application.Reminders;

public sealed class ReminderSettingsValidator : AbstractValidator<ReminderSettingsDto>
{
    public ReminderSettingsValidator()
    {
        RuleFor(s => s.Times)
            .NotNull().WithMessage("Die Uhrzeiten fehlen.")
            .Must(times => times is null || times.Count <= Reminder.MaxPerUser)
            .WithMessage($"Es sind höchstens {Reminder.MaxPerUser} Erinnerungen erlaubt.")
            .Must(times => times is null || times.Distinct().Count() == times.Count)
            .WithMessage("Die Uhrzeiten müssen eindeutig sein.");

        RuleForEach(s => s.Times)
            .Must(time => TimeOnly.TryParseExact(time, ReminderHandler.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .WithMessage("Jede Uhrzeit muss im Format HH:mm angegeben werden.");

        RuleFor(s => s.TimeZoneId)
            .NotEmpty().WithMessage("Die Zeitzone fehlt.")
            .Must(IsKnownTimeZone).WithMessage("Die Zeitzone ist unbekannt (IANA-Kennung, z. B. Europe/Berlin).");
    }

    private static bool IsKnownTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
