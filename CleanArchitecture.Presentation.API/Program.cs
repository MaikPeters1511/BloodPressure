using CleanArchitecture.Application;
using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Wolverine;
using JasperFx.CodeGeneration.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructureDI();
builder.Services.AddApplicationDI();

builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(BloodPressureHandler).Assembly);
    // Aspire registriert den DbContext über eine Factory (AddDbContextPool); Wolverine muss ihn daher
    // aus dem Container auflösen dürfen.
    options.ServiceLocationPolicy = ServiceLocationPolicy.AllowedButWarn;
});

builder.Services
    .AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
        // Anmeldung erst nach Bestätigung der E-Mail-Adresse (ADR 0006); bestehende Konten setzt eine Migration auf bestätigt.
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = builder.Configuration.GetValue("Auth:PasswordMinLength", 12);
    })
    .AddEntityFrameworkStores<AddPostgresSqlDbContext>();
// Explizite Lebensdauer statt Framework-Standard (1 h / 14 Tage); per Konfiguration anpassbar.
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(builder.Configuration.GetValue("Auth:AccessTokenMinutes", 30));
    options.RefreshTokenExpiration = TimeSpan.FromDays(builder.Configuration.GetValue("Auth:RefreshTokenDays", 7));
});
builder.Services.AddAuthorization();

// Der Scheduler für Erinnerungen läuft im Hintergrund; per Konfiguration abschaltbar (z. B. in Tests).
if (builder.Configuration.GetValue("Reminders:WorkerEnabled", true))
{
    builder.Services.AddHostedService<ReminderWorker>();
}

builder.Services.AddControllers(options => options.Filters.Add<ValidateRequestFilter>());
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(ProblemExamplesTransformer.TransformAsync);
    options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Access-Token aus POST /api/auth/login"
    };
    return Task.CompletedTask;
});
});
builder.Services.AddProblemDetails();

// Rate Limiting je Nutzer (angemeldet) bzw. je IP (anonym); Health-Probes sind ausgenommen.
var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimitPerMinute", 100);
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimitPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/alive") || context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("probe");
        }

        // Anmeldung, Registrierung und Token-Erneuerung: strengeres Limit je IP gegen Passwort-Raten.
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"auth:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        var key = context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Security")
            .LogWarning("Rate limit exceeded for {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 429, Title = "Zu viele Anfragen" }, cancellationToken);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
{
    if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    }
    else
    {
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    }
}));

if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });
}

var app = builder.Build();

// Hinter einem Reverse Proxy die echte Client-IP verwenden (Rate Limiting). Nur mit konfigurierten Proxys,
// sonst ließe sich die IP per Header fälschen: ForwardedHeaders:Enabled=true und ForwardedHeaders:KnownProxies=[...].
if (builder.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        forwarded.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }

    app.UseForwardedHeaders(forwarded);
}

app.UseExceptionHandler();

// Unversionierte Pfade leiten dauerhaft (308, Methode und Query bleiben erhalten) auf v1 weiter.
string[] legacyPrefixes = ["/api/BloodPressure", "/api/reminders", "/api/notifications"];
app.Use(async (context, next) =>
{
    foreach (var prefix in legacyPrefixes)
    {
        if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase, out var rest))
        {
            context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
            context.Response.Headers.Location = $"/api/v1{prefix["/api".Length..]}{rest}{context.Request.QueryString}";
            return;
        }
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "BloodPressure API v1"));

    // Schema lokal automatisch anlegen (Aspire-Postgres-Container startet leer).
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AddPostgresSqlDbContext>().Database.MigrateAsync();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<SecurityEventLoggingMiddleware>();
app.UseAuthorization();


app.MapDefaultEndpoints();
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>()
    .AddEndpointFilter(async (invocation, next) =>
    {
        var http = invocation.HttpContext;
        var path = http.Request.Path.Value ?? string.Empty;

        // Zwei-Faktor-Anmeldung ändern: Solange sie eingeschaltet ist, verlangen Ausschalten, neuer Schlüssel und neue
        // Wiederherstellungscodes einen gültigen Code aus der Authenticator-App. Sonst könnte jeder, der ein Access-Token
        // in die Hände bekommt, den zweiten Faktor entfernen (Identity prüft das selbst nicht).
        if (path.EndsWith("/manage/2fa", StringComparison.OrdinalIgnoreCase)
            && invocation.Arguments.OfType<TwoFactorRequest>().FirstOrDefault() is { } change
            && (change.Enable == false || change.ResetSharedKey || change.ResetRecoveryCodes))
        {
            var users = http.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            if (await users.GetUserAsync(http.User) is { TwoFactorEnabled: true } account
                && (string.IsNullOrWhiteSpace(change.TwoFactorCode)
                    || !await users.VerifyTwoFactorTokenAsync(account, users.Options.Tokens.AuthenticatorTokenProvider, change.TwoFactorCode)))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["twoFactorCode"] = ["Ein gültiger Code aus der Authenticator-App ist erforderlich."]
                });
            }
        }

        var result = await next(invocation);
        var inner = result is INestedHttpResult { Result: var nested } ? nested : result;

        // Login eines unbestätigten Kontos: "NotAllowed" nur zeigen, wenn das Passwort stimmt. Sonst wäre an der Antwort
        // zu erkennen, dass die Adresse registriert ist (unbekannte Adresse und falsches Passwort ergeben "Failed").
        if (path.EndsWith("/login", StringComparison.OrdinalIgnoreCase)
            && inner is ProblemHttpResult { ProblemDetails.Detail: "NotAllowed" }
            && invocation.Arguments.OfType<LoginRequest>().FirstOrDefault() is { } login)
        {
            var users = http.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            var user = await users.FindByEmailAsync(login.Email);
            if (user is null || !await users.CheckPasswordAsync(user, login.Password))
            {
                return TypedResults.Problem("Failed", statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        // Altdaten ohne Besitzer gehören dem ersten Nutzer, der seine E-Mail-Adresse bestätigt (nicht schon bei der Registrierung).
        if (path.EndsWith("/confirmEmail", StringComparison.OrdinalIgnoreCase)
            && inner is IStatusCodeHttpResult { StatusCode: null or (>= 200 and < 300) } // ohne Angabe: 200
            && http.Request.Query["userId"].ToString() is { Length: > 0 } confirmedUserId)
        {
            await http.RequestServices.GetRequiredService<ILegacyDataClaimer>()
                .ClaimForFirstUserAsync(confirmedUserId, http.RequestAborted);
        }

        return result;
    });
// Abmelden: rotiert den SecurityStamp. Dadurch werden alle Refresh-Tokens des Nutzers ungültig
// (die Identity-API prüft ihn beim Erneuern); ein bereits ausgestelltes Access-Token läuft erst nach seiner Lebensdauer ab.
app.MapPost("/api/auth/logout", async (ClaimsPrincipal principal, UserManager<IdentityUser> users) =>
{
    if (await users.GetUserAsync(principal) is { } user)
    {
        await users.UpdateSecurityStampAsync(user);
    }

    return Results.NoContent();
}).RequireAuthorization();
app.MapControllers();

app.Run();

// Für WebApplicationFactory in Integrationstests.
public partial class Program;
