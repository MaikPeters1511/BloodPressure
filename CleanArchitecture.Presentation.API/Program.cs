using CleanArchitecture.Application;
using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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
    })
    .AddEntityFrameworkStores<AddPostgresSqlDbContext>();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
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
}));
builder.Services.AddProblemDetails();
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

app.UseExceptionHandler();

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
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();


app.MapDefaultEndpoints();
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>()
    .AddEndpointFilter(async (invocation, next) =>
    {
        var result = await next(invocation);

        // Altdaten ohne Besitzer gehören dem ersten registrierten Nutzer.
        var http = invocation.HttpContext;
        if (http.Request.Path.Value?.EndsWith("/register", StringComparison.OrdinalIgnoreCase) == true
            && (result is INestedHttpResult { Result: var inner } ? inner : result) is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }
            && invocation.Arguments.OfType<RegisterRequest>().FirstOrDefault() is { } registration
            && await http.RequestServices.GetRequiredService<UserManager<IdentityUser>>()
                .FindByEmailAsync(registration.Email) is { } user)
        {
            await http.RequestServices.GetRequiredService<ILegacyDataClaimer>()
                .ClaimForFirstUserAsync(user.Id, http.RequestAborted);
        }

        return result;
    });
app.MapControllers();

app.Run();

// Für WebApplicationFactory in Integrationstests.
public partial class Program;
