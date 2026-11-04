using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure;

// Pro Schicht eine Extension-Methode; Aufruf in Program.cs:
//   builder.Services.AddApplicationDI().AddInfrastructureDI(builder.Configuration);
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureDI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<[Name]DbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' fehlt.")));

        // Scoped: gleiche Lebensdauer wie der DbContext (nie Singleton, sonst Captive Dependency).
        services.AddScoped<I[Name]Repository, [Name]Repository>();

        return services;
    }
}
