using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using FluentValidation;

namespace CleanArchitecture.Application.BloodPressures;

/// <summary>Prüft Anlegen und Ändern einer Messung und meldet alle Feldfehler gemeinsam.</summary>
public sealed class BloodPressureRequestValidator : AbstractValidator<BloodPressureRequest>
{
    public BloodPressureRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(r => r.Systolisch)
            .InclusiveBetween(BloodPressureLimits.PressureMin, BloodPressureLimits.PressureMax)
            .WithMessage($"Systolisch muss zwischen {BloodPressureLimits.PressureMin} und {BloodPressureLimits.PressureMax} liegen.");

        RuleFor(r => r.Diastolisch)
            .InclusiveBetween(BloodPressureLimits.PressureMin, BloodPressureLimits.PressureMax)
            .WithMessage($"Diastolisch muss zwischen {BloodPressureLimits.PressureMin} und {BloodPressureLimits.PressureMax} liegen.");

        RuleFor(r => r.Systolisch)
            .GreaterThan(r => r.Diastolisch)
            .WithMessage("Systolisch muss größer als Diastolisch sein.");

        RuleFor(r => r.Pulse)
            .InclusiveBetween(BloodPressureLimits.PulseMin, BloodPressureLimits.PulseMax)
            .WithMessage($"Puls muss zwischen {BloodPressureLimits.PulseMin} und {BloodPressureLimits.PulseMax} liegen.");

        RuleFor(r => r.Oxygen)
            .InclusiveBetween(BloodPressureLimits.OxygenMin, BloodPressureLimits.OxygenMax)
            .WithMessage($"Sauerstoffsättigung muss zwischen {BloodPressureLimits.OxygenMin} und {BloodPressureLimits.OxygenMax} liegen.");

        RuleFor(r => r.DateTime)
            .Must(value => value <= timeProvider.GetUtcNow().UtcDateTime + BloodPressureLimits.FutureTolerance)
            .WithMessage("Der Messzeitpunkt darf nicht in der Zukunft liegen.");
    }
}
