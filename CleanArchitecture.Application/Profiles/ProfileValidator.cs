using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using FluentValidation;

namespace CleanArchitecture.Application.Profiles;

public sealed class ProfileValidator : AbstractValidator<ProfileDto>
{
    public ProfileValidator()
    {
        RuleFor(p => p.Age)
            .InclusiveBetween(ProfileLimits.MinAge, ProfileLimits.MaxAge)
            .When(p => p.Age is not null)
            .WithMessage($"Das Alter muss zwischen {ProfileLimits.MinAge} und {ProfileLimits.MaxAge} Jahren liegen.");

        RuleFor(p => p.HeightCm)
            .InclusiveBetween(ProfileLimits.MinHeightCm, ProfileLimits.MaxHeightCm)
            .When(p => p.HeightCm is not null)
            .WithMessage($"Die Größe muss zwischen {ProfileLimits.MinHeightCm} und {ProfileLimits.MaxHeightCm} cm liegen.");

        RuleFor(p => p.WeightKg)
            .Must(w => w >= ProfileLimits.MinWeightKg && w <= ProfileLimits.MaxWeightKg && decimal.Round(w!.Value, 1) == w)
            .When(p => p.WeightKg is not null)
            .WithMessage($"Das Gewicht muss zwischen {ProfileLimits.MinWeightKg:0} und {ProfileLimits.MaxWeightKg:0} kg liegen und höchstens eine Nachkommastelle haben.");
    }
}
