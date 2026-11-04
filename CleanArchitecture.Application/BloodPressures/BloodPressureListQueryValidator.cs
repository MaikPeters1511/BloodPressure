using CleanArchitecture.Contracts;
using FluentValidation;

namespace CleanArchitecture.Application.BloodPressures;

public sealed class BloodPressureListQueryValidator : AbstractValidator<BloodPressureListQuery>
{
    public BloodPressureListQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Die Seite muss mindestens 1 sein.");

        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1).WithMessage("Die Seitengröße muss mindestens 1 sein.");

        RuleFor(q => q.Sort)
            .Must(sort => string.Equals(sort, BloodPressureListQuery.SortDateAscending, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(sort, BloodPressureListQuery.SortDateDescending, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Sortierung muss '{BloodPressureListQuery.SortDateAscending}' oder '{BloodPressureListQuery.SortDateDescending}' sein.");

        RuleFor(q => q.From)
            .Must((q, from) => from is null || q.To is null || from <= q.To)
            .WithMessage("'from' darf nicht nach 'to' liegen.");
    }
}
