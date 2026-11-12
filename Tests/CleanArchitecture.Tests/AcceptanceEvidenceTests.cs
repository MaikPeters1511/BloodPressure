using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecture.Tests;

/// <summary>Belege für Abnahmepunkte aus Epic 3: Grenzwerte nur an einer Stelle, keine internen Details in Antworten.</summary>
public class AcceptanceEvidenceTests
{
    [Test]
    [Category("Unit")]
    [Arguments(BloodPressureLimits.PressureMin, true)]
    [Arguments(BloodPressureLimits.PressureMin - 1, false)]
    [Arguments(BloodPressureLimits.PressureMax, true)]
    [Arguments(BloodPressureLimits.PressureMax + 1, false)]
    public async Task Pressure_ValidatorAndDomain_AgreeOnBoundaries(int value, bool valid)
    {
        var validator = new BloodPressureRequestValidator(TimeProvider.System);
        var request = new BloodPressureRequest(value, 80, DateTime.UtcNow.AddMinutes(-1), 60, 98);

        var validatorAccepts = (await validator.ValidateAsync(request)).Errors
            .All(e => e.PropertyName != nameof(BloodPressureRequest.Systolisch) || !e.ErrorMessage.Contains("liegen", StringComparison.Ordinal));
        var domainAccepts = Record.Exception(() => new BloodPressure { Systolisch = value }) is null;

        await Assert.That(validatorAccepts).IsEqualTo(valid);
        await Assert.That(domainAccepts).IsEqualTo(valid);
    }

    [Test]
    [Category("Unit")]
    public async Task Limits_AreDefinedInOnePlaceOnly()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                        && !f.EndsWith("BloodPressureLimits.cs", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("const int PressureMax", StringComparison.Ordinal)
                        || File.ReadAllText(f).Contains("const int PulseMax", StringComparison.Ordinal))
            .ToList();

        await Assert.That(offenders).IsEmpty();
    }

    [Test]
    [Category("Integration")]
    public async Task UnhandledException_ReturnsProblemWithoutInternals()
    {
        await using var factory = new ApiFactory();
        using var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IBloodPressureRepository>();
            s.AddSingleton(DispatchProxy.Create<IBloodPressureRepository, ThrowingProxy>());
        })).CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "user-a");

        var response = await client.GetAsync("/api/v1/BloodPressure");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(body).DoesNotContain("secret-internal-detail");
        await Assert.That(body).DoesNotContain("   at ");
        await Assert.That(body).DoesNotContain("InvalidOperationException");
    }

    [Test]
    [Category("Integration")]
    public async Task ValidationError_ReturnsNoStackTrace()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClientFor("user-a");

        var response = await client.PostAsJsonAsync("/api/v1/BloodPressure",
            new BloodPressureRequest(BloodPressureLimits.PressureMax + 1, 80, DateTime.UtcNow.AddMinutes(-1), 60, 98));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body).DoesNotContain("   at ");
        await Assert.That(body).DoesNotContain("Exception");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CleanArchitecture.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }

    private static class Record
    {
        public static Exception? Exception(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    public class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("secret-internal-detail");
    }
}
