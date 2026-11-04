using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class SecurityConventionTests
{
    [Test]
    public async Task EveryController_DeclaresAuthorizeOrAllowAnonymous()
    {
        var controllers = typeof(BloodPressureController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        var unprotected = controllers
            .Where(t => !t.IsDefined(typeof(AuthorizeAttribute), true) && !t.IsDefined(typeof(AllowAnonymousAttribute), true))
            .Select(t => t.Name)
            .ToList();

        await Assert.That(unprotected).IsEmpty();
    }

    [Test]
    public async Task BloodPressureController_IsNotAnonymous()
    {
        await Assert.That(typeof(BloodPressureController).IsDefined(typeof(AllowAnonymousAttribute), true)).IsFalse();
        await Assert.That(typeof(BloodPressureController).IsDefined(typeof(AuthorizeAttribute), true)).IsTrue();
    }

    [Test]
    public async Task BearerHandler_WithTokenProvider_SendsAuthorizationHeader()
    {
        var capture = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IAccessTokenProvider>(new FixedTokenProvider("abc123"));
        services.AddBloodPressureApiClient(new Uri("https://api.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => capture);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IBloodPressureApi>().GetAllAsync();

        await Assert.That(capture.Authorization).IsEqualTo("Bearer abc123");
    }

    [Test]
    public async Task BearerHandler_WithoutTokenProvider_SendsNoAuthorizationHeader()
    {
        var capture = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddBloodPressureApiClient(new Uri("https://api.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => capture);
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IBloodPressureApi>().GetAllAsync();

        await Assert.That(capture.Authorization).IsNull();
    }

    private sealed class FixedTokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(token);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"page\":1,\"pageSize\":50,\"totalCount\":0}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
