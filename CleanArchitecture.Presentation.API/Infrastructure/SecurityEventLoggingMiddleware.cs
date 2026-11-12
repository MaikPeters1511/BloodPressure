namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>
/// Protokolliert abgelehnte Zugriffe (401/403) als Sicherheitsereignis, ohne Token, Passwörter oder Nutzerdaten.
/// </summary>
public sealed class SecurityEventLoggingMiddleware(RequestDelegate next, ILogger<SecurityEventLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            logger.LogWarning("Access denied with {StatusCode} for {Method} {Path}",
                context.Response.StatusCode, context.Request.Method, context.Request.Path);
        }
    }
}
