using CleanArchitecture.Application;
using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using CleanArchitecture.Presentation.API.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
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
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

// Für WebApplicationFactory in Integrationstests.
public partial class Program;
