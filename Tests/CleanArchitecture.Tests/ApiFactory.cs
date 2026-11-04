using CleanArchitecture.Domain.IRepository;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecture.Tests;

/// <summary>Startet die API im Speicher; das Postgres-Repository wird durch ein Fake ersetzt.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeBloodPressureRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Nicht "Development": dort würde die Migration gegen Postgres laufen.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:bloodpressuredb"] = "Host=localhost;Database=unused;Username=unused;Password=unused"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBloodPressureRepository>();
            services.AddSingleton<IBloodPressureRepository>(Repository);
        });
    }
}
