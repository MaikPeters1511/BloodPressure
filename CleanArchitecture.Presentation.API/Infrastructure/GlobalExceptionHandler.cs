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
        if (exception is ArgumentOutOfRangeException)
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
