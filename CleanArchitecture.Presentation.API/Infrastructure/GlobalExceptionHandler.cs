using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>
/// Zentrales Exception Handling: Domain-Validierungsfehler werden zu 400,
/// alles andere zu 500 – ohne Stacktrace oder interne Details nach außen.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (exception is PreconditionFailedException)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status412PreconditionFailed,
                Title = "Veralteter Stand",
                Detail = exception.Message
            };
        }
        else if (exception is ConcurrencyConflictException)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Konflikt",
                Detail = exception.Message
            };
        }
        else if (exception is ExportLimitExceededException)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Export zu groß",
                Detail = exception.Message
            };
        }
        else if (exception is ArgumentOutOfRangeException)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ungültige Eingabe",
                Detail = exception.Message
            };
        }
        else
        {
            logger.LogError(exception, "Unbehandelte Ausnahme bei {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Interner Serverfehler"
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
