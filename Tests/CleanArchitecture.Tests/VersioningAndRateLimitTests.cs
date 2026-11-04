using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class VersioningAndRateLimitTests
{
    private static HttpClient NoRedirectClient(ApiFactory factory, string user = "user-a")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user);
        return client;
    }

    [Test]
    public async Task V1Path_ServesTheApi()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClientFor("user-a").GetAsync("/api/v1/BloodPressure");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("/api/BloodPressure?page=2&pageSize=5", "/api/v1/BloodPressure?page=2&pageSize=5")]
    [Arguments("/api/bloodpressure/7", "/api/v1/BloodPressure/7")]
    [Arguments("/api/BloodPressure/statistics?interval=week", "/api/v1/BloodPressure/statistics?interval=week")]
    [Arguments("/api/reminders", "/api/v1/reminders")]
    [Arguments("/api/notifications", "/api/v1/notifications")]
    public async Task UnversionedPath_RedirectsPermanentlyToV1(string legacy, string expected)
    {
        await using var factory = new ApiFactory();

        var response = await NoRedirectClient(factory).GetAsync(legacy);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PermanentRedirect);
        await Assert.That(response.Headers.Location!.OriginalString).IsEqualTo(expected);
    }

    [Test]
    public async Task Redirect_PreservesTheMethod_ForWrites()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClientFor("user-a");

        var response = await client.PostAsJsonAsyncNoRedirect(factory);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PermanentRedirect);
    }

    [Test]
    public async Task AuthEndpoints_AreNotRedirected()
    {
        await using var factory = new ApiFactory();

        var response = await NoRedirectClient(factory).GetAsync("/api/auth/manage/info");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpStatusCode.PermanentRedirect);
    }

    [Test]
    public async Task ExceedingTheLimit_Returns429WithRetryAfter()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string> { ["RateLimiting:PermitLimitPerMinute"] = "3" });
        var client = factory.CreateClientFor("user-a");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.GetAsync("/api/v1/BloodPressure")).StatusCode);
        }

        var rejected = await client.GetAsync("/api/v1/BloodPressure");
        await Assert.That(statuses.Take(3).All(s => s == HttpStatusCode.OK)).IsTrue();
        await Assert.That(statuses[3]).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(rejected.Headers.RetryAfter).IsNotNull();
        await Assert.That(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Limit_IsPerUser()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string> { ["RateLimiting:PermitLimitPerMinute"] = "2" });
        var userA = factory.CreateClientFor("user-a");
        var userB = factory.CreateClientFor("user-b");
        for (var i = 0; i < 3; i++)
        {
            await userA.GetAsync("/api/v1/BloodPressure");
        }

        var responseB = await userB.GetAsync("/api/v1/BloodPressure");

        await Assert.That(responseB.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task HealthProbes_AreNotRateLimited()
    {
        await using var factory = new ApiFactory(settings: new Dictionary<string, string> { ["RateLimiting:PermitLimitPerMinute"] = "2" });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            statuses.Add((await client.GetAsync("/alive")).StatusCode);
        }

        await Assert.That(string.Join(",", statuses)).IsEqualTo("OK,OK,OK,OK,OK,OK");
    }
}

internal static class RedirectTestExtensions
{
    public static async Task<HttpResponseMessage> PostAsJsonAsyncNoRedirect(this HttpClient _, ApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "user-a");
        return await client.PostAsync("/api/BloodPressure", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
    }
}
