using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Tests;

/// <summary>Belege zur OWASP-Prüfung von Epic 1 (R-02, R-03, R-05, R-12).</summary>
[Category("Integration")]
public class SecurityHardeningTests
{
    // Ausdrücklich freigegebene anonyme Endpunkte (docs/security/owasp-epic-01.md, Abschnitt 3).
    // Die letzten vier registriert MapIdentityApi nach Framework-Standard; sie sind hier bewusst aufgeführt.
    private static readonly string[] AllowedAnonymous =
    [
        "/alive",
        "/api/auth/register",
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/confirmEmail",
        "/api/auth/resendConfirmationEmail",
        "/api/auth/forgotPassword",
        "/api/auth/resetPassword"
    ];

    [Test]
    public async Task OnlyAllowedEndpoints_AreAnonymous()
    {
        await using var factory = new ApiFactory();
        _ = factory.CreateClient();

        var anonymous = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null || e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => "/" + e.RoutePattern.RawText?.TrimStart('/'))
            .Where(path => !path.StartsWith("/openapi", StringComparison.Ordinal))
            .Distinct()
            .Order()
            .ToList();

        await Assert.That(anonymous).Contains("/alive");
        await Assert.That(anonymous).Contains("/api/auth/login");
        await Assert.That(anonymous.Except(AllowedAnonymous, StringComparer.OrdinalIgnoreCase).ToList()).IsEmpty();
    }

    [Test]
    public async Task Responses_CarrySecurityHeaders()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClientFor("user-a").GetAsync("/api/v1/BloodPressure");

        await Assert.That(response.Headers.GetValues("X-Content-Type-Options").Single()).IsEqualTo("nosniff");
        await Assert.That(response.Headers.GetValues("Referrer-Policy").Single()).IsEqualTo("no-referrer");
        await Assert.That(response.Headers.GetValues("Content-Security-Policy").Single()).Contains("frame-ancestors 'none'");
    }

    [Test]
    public async Task AuthEndpoints_HaveStricterRateLimit()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string>
        {
            ["RateLimiting:AuthPermitLimitPerMinute"] = "2",
            ["RateLimiting:PermitLimitPerMinute"] = "1000"
        });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.GetAsync("/api/auth/unknown")).StatusCode);
        }

        await Assert.That(statuses[2]).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(statuses[0]).IsNotEqualTo(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task AccessDenied_IsLogged_WithoutCredentials()
    {
        await using var factory = new ApiFactory();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/BloodPressure");
        request.Headers.Add("Authorization", "Bearer secret-token-value");
        _ = await factory.CreateClient().SendAsync(request);

        var messages = factory.Logs.Entries.Select(e => e.Message).ToList();
        await Assert.That(messages.Any(m => m.Contains("Access denied with 401", StringComparison.Ordinal))).IsTrue();
        await Assert.That(messages.Any(m => m.Contains("secret-token-value", StringComparison.Ordinal))).IsFalse();
    }
}
