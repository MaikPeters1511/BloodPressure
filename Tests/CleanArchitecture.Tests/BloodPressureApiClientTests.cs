using CleanArchitecture.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class BloodPressureApiClientTests
{
    private ApiFactory _factory = null!;
    private IBloodPressureApi _api = null!;

    private static readonly DateTime Measured = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    [Before(Test)]
    public void Setup()
    {
        _factory = new ApiFactory();
        _api = RestService.For<IBloodPressureApi>(_factory.CreateClientFor("user-a"));
    }

    [After(Test)]
    public async Task Teardown() => await _factory.DisposeAsync();

    [Test]
    public async Task GetAll_WithStoredMeasurement_ReturnsIt()
    {
        await _api.CreateAsync(new BloodPressureRequest(120, 80, Measured, 65, 98));

        var all = await _api.GetAllAsync();

        await Assert.That(all.TotalCount).IsEqualTo(1);
        await Assert.That(all.Items[0].Systolisch).IsEqualTo(120);
    }

    [Test]
    public async Task Create_ValidRequest_ReturnsDtoWithId()
    {
        var created = await _api.CreateAsync(new BloodPressureRequest(120, 80, Measured, 65, 98));

        await Assert.That(created.Id).IsGreaterThan(0);
    }

    [Test]
    public async Task UpdateAndDelete_ExistingMeasurement_DoNotThrow()
    {
        var created = await _api.CreateAsync(new BloodPressureRequest(120, 80, Measured, 65, 98));

        await _api.UpdateAsync(created.Id, new BloodPressureRequest(130, 85, Measured, 70, 97));
        await _api.DeleteAsync(created.Id);

        await Assert.That(_factory.Repository.Find(created.Id)).IsNull();
    }

    [Test]
    public async Task Create_InvalidSystolisch_ThrowsApiException400WithProblemDetails()
    {
        var ex = await Assert.That(async () => await _api.CreateAsync(new BloodPressureRequest(999, 80, Measured, 65, 98)))
            .Throws<ApiException>();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(400);
        var problem = await ex.GetContentAsAsync<ProblemDetailsDto>();
        await Assert.That(problem!.Title).IsEqualTo("Ungültige Eingabe");
    }

    [Test]
    public async Task Create_InvalidFields_ApiExceptionExposesFieldErrors()
    {
        var ex = await Assert.That(async () => await _api.CreateAsync(new BloodPressureRequest(70, 90, Measured, 65, 150)))
            .Throws<ApiException>();

        var problem = await ex!.GetContentAsAsync<ValidationProblemDto>();
        await Assert.That(problem!.Errors.Keys).Contains("systolisch");
        await Assert.That(problem.Errors.Keys).Contains("oxygen");
    }

    [Test]
    public async Task GetById_Unknown_ThrowsApiException404()
    {
        var ex = await Assert.That(async () => await _api.GetByIdAsync(99)).Throws<ApiException>();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task RoundTrip_ReturnsAllFieldsUnchanged()
    {
        var created = await _api.CreateAsync(new BloodPressureRequest(121, 79, Measured, 66, 97));

        var loaded = await _api.GetByIdAsync(created.Id);

        await Assert.That(loaded).IsEqualTo(new BloodPressureDto(created.Id, 121, 79, Measured, 66, 97, "Normal"));
    }

    [Test]
    public async Task AddBloodPressureApiClient_UsesConfiguredBaseAddress()
    {
        var handler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddBloodPressureApiClient(new Uri("https://api.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IBloodPressureApi>();
        await client.GetAllAsync();

        await Assert.That(handler.RequestUri).IsEqualTo(new Uri("https://api.example.test/api/BloodPressure"));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"page\":1,\"pageSize\":50,\"totalCount\":0}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record ProblemDetailsDto(string? Title, int? Status, string? Detail);

    private sealed record ValidationProblemDto(string? Title, Dictionary<string, string[]> Errors);
}
