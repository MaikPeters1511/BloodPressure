using CleanArchitecture.Contracts;
using FluentValidation;

namespace CleanArchitecture.Application.Export;

public sealed class ExportQueryValidator : AbstractValidator<ExportQuery>
{
    public ExportQueryValidator()
    {
        RuleFor(q => q.Format)
            .Must(value => string.Equals(value, ExportQuery.Csv, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(value, ExportQuery.Pdf, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Das Format muss {ExportQuery.Csv} oder {ExportQuery.Pdf} sein.");

        RuleFor(q => q.From)
            .Must((q, from) => from is null || q.To is null || from <= q.To)
            .WithMessage("'from' darf nicht nach 'to' liegen.");
    }
}
