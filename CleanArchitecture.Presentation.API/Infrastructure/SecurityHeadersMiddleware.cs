namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>
/// Setzt Sicherheits-Header für alle Antworten der API. Die strenge CSP gilt nicht in Development,
/// weil die Swagger-Oberfläche dort Skripte und Styles nachlädt.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            if (!environment.IsDevelopment())
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
