using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using Refit;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class ListQueryTests
{
    private ApiFactory _factory = null!;
    private IBloodPressureApi _api = null!;
    private HttpClient _http = null!;

    private static DateTime Day(int day) => new(2026, 11, day, 8, 0, 0, DateTimeKind.Utc);

    [Before(Test)]
    public void Setup()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClientFor("user-a");
        _api = RestService.For<IBloodPressureApi>(_http);
    }

    [After(Test)]
    public async Task Teardown() => await _factory.DisposeAsync();

    private async Task SeedAsync(params DateTime[] times)
    {
        foreach (var time in times)
        {
            await _factory.Repository.AddAsync(new BloodPressure
            {
                UserId = "user-a", Systolisch = 120, Diastolisch = 80, DateTime = time, Pulse = 60, Oxygen = 98
            });
        }
    }

    [Test]
    public async Task From_To_ReturnsOnlyMeasurementsInRange()
    {
        await SeedAsync(Day(1), Day(5), Day(10));

        var result = await _api.GetAllAsync(from: Day(4), to: Day(6));

        await Assert.That(result.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].DateTime).IsEqualTo(Day(5));
    }

    [Test]
    public async Task From_To_BoundariesAreInclusive()
    {
        await SeedAsync(Day(1), Day(5), Day(10));

        var result = await _api.GetAllAsync(from: Day(1), to: Day(10));

        await Assert.That(result.TotalCount).IsEqualTo(3);
    }

    [Test]
    public async Task FromAfterTo_Returns400()
    {
        var response = await _http.GetAsync("/api/BloodPressure?from=2026-11-10T00:00:00Z&to=2026-11-01T00:00:00Z");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task SecondPage_Returns50Items_AndTotal120()
    {
        await SeedAsync(Enumerable.Range(0, 120).Select(i => Day(1).AddMinutes(i)).ToArray());

        var result = await _api.GetAllAsync(page: 2, pageSize: 50);

        await Assert.That(result.Items.Count).IsEqualTo(50);
        await Assert.That(result.TotalCount).IsEqualTo(120);
        await Assert.That(result.Page).IsEqualTo(2);
    }

    [Test]
    public async Task LastPage_ReturnsRemainingItems()
    {
        await SeedAsync(Enumerable.Range(0, 120).Select(i => Day(1).AddMinutes(i)).ToArray());

        var result = await _api.GetAllAsync(page: 3, pageSize: 50);

        await Assert.That(result.Items.Count).IsEqualTo(20);
    }

    [Test]
    public async Task PageSizeAboveMaximum_IsClampedTo200()
    {
        await SeedAsync(Enumerable.Range(0, 250).Select(i => Day(1).AddMinutes(i)).ToArray());

        var result = await _api.GetAllAsync(pageSize: 1000);

        await Assert.That(result.PageSize).IsEqualTo(200);
        await Assert.That(result.Items.Count).IsEqualTo(200);
        await Assert.That(result.TotalCount).IsEqualTo(250);
    }

    [Test]
    public async Task DefaultSort_IsNewestFirst()
    {
        await SeedAsync(Day(1), Day(5), Day(3));

        var result = await _api.GetAllAsync();

        await Assert.That(result.Items.Select(i => i.DateTime).ToArray()).IsEquivalentTo(new[] { Day(5), Day(3), Day(1) });
        await Assert.That(result.Items[0].DateTime).IsEqualTo(Day(5));
        await Assert.That(result.PageSize).IsEqualTo(50);
    }

    [Test]
    public async Task SortDateTime_IsOldestFirst()
    {
        await SeedAsync(Day(1), Day(5), Day(3));

        var result = await _api.GetAllAsync(sort: "dateTime");

        await Assert.That(result.Items[0].DateTime).IsEqualTo(Day(1));
        await Assert.That(result.Items[2].DateTime).IsEqualTo(Day(5));
    }

    [Test]
    [Arguments("page=0")]
    [Arguments("pageSize=0")]
    [Arguments("sort=systolisch")]
    public async Task InvalidParameters_Return400(string query)
    {
        var response = await _http.GetAsync($"/api/BloodPressure?{query}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
