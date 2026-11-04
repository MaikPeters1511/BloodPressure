using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Contracts;
using Refit;

namespace CleanArchitecture.Tests;

/// <summary>Optimistic Concurrency über ETag/If-Match gegen die echte Zeilenversion (xmin) in PostgreSQL.</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class ConcurrencyTests(PostgresFixture postgres)
{
    private static readonly DateTime Measured = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly BloodPressureRequest Request = new(120, 80, Measured, 65, 98);
    private static readonly BloodPressureRequest Changed = new(130, 85, Measured, 70, 97);

    private async Task<(ApiFactory Factory, HttpClient Http, IBloodPressureApi Api, BloodPressureDto Created)> StartAsync()
    {
        var factory = await RealApiHarness.StartAsync(postgres);
        var http = await RealApiHarness.SignInAsync(factory, "a@example.test");
        var api = RestService.For<IBloodPressureApi>(http);
        return (factory, http, api, await api.CreateAsync(Request));
    }

    [Test]
    public async Task Get_ReturnsEtagMatchingTheVersionInTheBody()
    {
        var (factory, http, _, created) = await StartAsync();
        await using var _ = factory;

        var response = await http.GetAsync($"/api/v1/BloodPressure/{created.Id}");
        var body = await response.Content.ReadFromJsonAsync<BloodPressureDto>();

        await Assert.That(response.Headers.ETag!.Tag).IsEqualTo(body!.ETag);
        await Assert.That(body.Version).IsGreaterThan(0u);
    }

    [Test]
    public async Task Update_WithCurrentEtag_SucceedsAndReturnsNewEtag()
    {
        var (factory, http, api, created) = await StartAsync();
        await using var _ = factory;

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/BloodPressure/{created.Id}") { Content = JsonContent.Create(Changed) };
        request.Headers.TryAddWithoutValidation("If-Match", created.ETag);
        var response = await http.SendAsync(request);
        var reloaded = await api.GetByIdAsync(created.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(response.Headers.ETag!.Tag).IsEqualTo(reloaded.ETag);
        await Assert.That(reloaded.ETag).IsNotEqualTo(created.ETag);
    }

    [Test]
    public async Task Update_WithStaleEtag_Returns412_AndKeepsTheOtherChange()
    {
        var (factory, _, api, created) = await StartAsync();
        await using var _ = factory;
        var staleEtag = created.ETag; // beide Geräte haben diesen Stand gelesen
        await api.UpdateAsync(created.Id, Changed, staleEtag); // Gerät A speichert zuerst

        var ex = await Assert.That(async () => await api.UpdateAsync(created.Id, Request with { Systolisch = 99, Diastolisch = 60 }, staleEtag))
            .Throws<ApiException>();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(412);
        await Assert.That((await api.GetByIdAsync(created.Id)).Systolisch).IsEqualTo(130);
    }

    [Test]
    public async Task Delete_WithStaleEtag_Returns412_AndKeepsTheMeasurement()
    {
        var (factory, _, api, created) = await StartAsync();
        await using var _ = factory;
        await api.UpdateAsync(created.Id, Changed, created.ETag);

        var ex = await Assert.That(async () => await api.DeleteAsync(created.Id, created.ETag)).Throws<ApiException>();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(412);
        await Assert.That((await api.GetAllAsync()).TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Delete_WithCurrentEtag_Succeeds()
    {
        var (factory, _, api, created) = await StartAsync();
        await using var _ = factory;
        var current = await api.GetByIdAsync(created.Id);

        await api.DeleteAsync(created.Id, current.ETag);

        await Assert.That((await api.GetAllAsync()).TotalCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(null)]
    [Arguments("*")]
    public async Task Update_WithoutOrWildcardIfMatch_IsUnconditional(string? ifMatch)
    {
        var (factory, _, api, created) = await StartAsync();
        await using var _ = factory;
        await api.UpdateAsync(created.Id, Changed, created.ETag); // macht den alten Stand veraltet

        await api.UpdateAsync(created.Id, Request, ifMatch);

        await Assert.That((await api.GetByIdAsync(created.Id)).Systolisch).IsEqualTo(120);
    }

    [Test]
    public async Task Update_WithMalformedIfMatch_Returns400()
    {
        var (factory, _, api, created) = await StartAsync();
        await using var _ = factory;

        var ex = await Assert.That(async () => await api.UpdateAsync(created.Id, Changed, "\"abc\"")).Throws<ApiException>();

        await Assert.That((int)ex!.StatusCode).IsEqualTo(400);
    }
}
