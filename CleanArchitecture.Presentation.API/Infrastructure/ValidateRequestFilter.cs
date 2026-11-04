using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>
/// Validiert alle Action-Argumente, für die ein <see cref="IValidator{T}"/> registriert ist, vor der Action und
/// antwortet bei Fehlern mit 400 und <see cref="ValidationProblemDetails"/> (alle Feldfehler, camelCase-Feldnamen).
/// </summary>
public sealed class ValidateRequestFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null
                || services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var field = JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName);
                if (!errors.TryGetValue(field, out var messages))
                {
                    errors[field] = messages = [];
                }

                if (!messages.Contains(failure.ErrorMessage))
                {
                    messages.Add(failure.ErrorMessage);
                }
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(
                new ValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()))
                {
                    Title = "Ungültige Eingabe",
                    Status = StatusCodes.Status400BadRequest
                });
            return;
        }

        await next();
    }
}
