using CleanArchitecture.Contracts;
using FluentValidation;

namespace CleanArchitecture.Application.BloodPressures;

public sealed class StatisticsQueryValidator : AbstractValidator<StatisticsQuery>
{
    private static readonly string[] Intervals = [StatisticsQuery.Day, StatisticsQuery.Week, StatisticsQuery.Month];

    public StatisticsQueryValidator()
    {
        RuleFor(q => q.Interval)
            .Must(value => Intervals.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Das Intervall muss {string.Join(", ", Intervals)} sein.");

        RuleFor(q => q.From)
            .Must((q, from) => from is null || q.To is null || from <= q.To)
            .WithMessage("'from' darf nicht nach 'to' liegen.");
    }
}
