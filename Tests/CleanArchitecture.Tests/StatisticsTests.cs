using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using Refit;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class StatisticsTests
{
    private ApiFactory _factory = null!;
    private IBloodPressureApi _api = null!;
    private HttpClient _http = null!;

    private static DateTime At(int month, int day, int hour = 8) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Before(Test)]
    public void Setup()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClientFor("user-a");
        _api = RestService.For<IBloodPressureApi>(_http);
    }

    [After(Test)]
    public async Task Teardown() => await _factory.DisposeAsync();

    private Task AddAsync(DateTime time, int systolic, int diastolic, int pulse = 60, string user = "user-a")
        => _factory.Repository.AddAsync(new BloodPressure
        {
            UserId = user, Systolisch = systolic, Diastolisch = diastolic, DateTime = time, Pulse = pulse, Oxygen = 98
        });

    [Test]
    public async Task Measurement_ContainsCategory()
    {
        var created = await _api.CreateAsync(new BloodPressureRequest(118, 68, At(10, 1), 65, 98));
        var high = await _api.CreateAsync(new BloodPressureRequest(185, 115, At(10, 2), 65, 98));

        await Assert.That(created.Category).IsEqualTo("NonElevated");
        await Assert.That(high.Category).IsEqualTo("Hypertension");
        await Assert.That((await _api.GetByIdAsync(high.Id)).Category).IsEqualTo("Hypertension");
    }

    [Test]
    public async Task DailyStatistics_CalculatesAverageMinMaxAndCount()
    {
        await AddAsync(At(10, 1, 8), 120, 80, 60);
        await AddAsync(At(10, 1, 20), 130, 90, 70);

        var buckets = await _api.GetStatisticsAsync(interval: "day");

        var day = buckets.Single();
        await Assert.That(day.Count).IsEqualTo(2);
        await Assert.That(day.Systolisch.Average).IsEqualTo(125.0);
        await Assert.That(day.Diastolisch.Average).IsEqualTo(85.0);
        await Assert.That(day.Systolisch.Min).IsEqualTo(120);
        await Assert.That(day.Systolisch.Max).IsEqualTo(130);
        await Assert.That(day.Diastolisch.Min).IsEqualTo(80);
        await Assert.That(day.Diastolisch.Max).IsEqualTo(90);
        await Assert.That(day.Pulse.Average).IsEqualTo(65.0);
        await Assert.That(day.PeriodStart).IsEqualTo(At(10, 1, 0));
    }

    [Test]
    public async Task DailyStatistics_OmitsDaysWithoutMeasurements()
    {
        await AddAsync(At(10, 1), 120, 80);
        await AddAsync(At(10, 4), 120, 80);

        var buckets = await _api.GetStatisticsAsync(interval: "day");

        await Assert.That(buckets.Count).IsEqualTo(2);
    }

    [Test]
    public async Task WeeklyStatistics_GroupsByIsoWeekStartingMonday()
    {
        // 2026-10-05 ist ein Montag. Mo/So derselben Woche, danach nächste Woche.
        await AddAsync(At(10, 5), 120, 80);
        await AddAsync(At(10, 11), 140, 90);
        await AddAsync(At(10, 12), 100, 60);

        var buckets = await _api.GetStatisticsAsync(interval: "week");

        await Assert.That(buckets.Count).IsEqualTo(2);
        await Assert.That(buckets[0].PeriodStart).IsEqualTo(At(10, 5, 0));
        await Assert.That(buckets[0].Count).IsEqualTo(2);
        await Assert.That(buckets[0].Systolisch.Average).IsEqualTo(130.0);
        await Assert.That(buckets[1].PeriodStart).IsEqualTo(At(10, 12, 0));
    }

    [Test]
    public async Task MonthlyStatistics_UsesWeightedAverageAcrossDays()
    {
        await AddAsync(At(9, 1), 100, 60);
        await AddAsync(At(9, 2), 100, 60);
        await AddAsync(At(9, 3), 130, 90);

        var buckets = await _api.GetStatisticsAsync(interval: "month");

        var month = buckets.Single();
        await Assert.That(month.PeriodStart).IsEqualTo(At(9, 1, 0));
        await Assert.That(month.Count).IsEqualTo(3);
        await Assert.That(month.Systolisch.Average).IsEqualTo(110.0);
    }

    [Test]
    public async Task Statistics_AreRestrictedToTimeRangeAndUser()
    {
        await AddAsync(At(10, 1), 120, 80);
        await AddAsync(At(10, 20), 120, 80);
        await AddAsync(At(10, 2), 200, 120, user: "user-b");

        var buckets = await _api.GetStatisticsAsync(from: At(10, 1, 0), to: At(10, 10, 0), interval: "day");

        await Assert.That(buckets.Count).IsEqualTo(1);
        await Assert.That(buckets[0].Systolisch.Max).IsEqualTo(120);
    }

    [Test]
    public async Task Statistics_WithoutMeasurements_ReturnsEmptyList()
    {
        var response = await _http.GetAsync("/api/v1/BloodPressure/statistics?interval=day");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("[]");
    }

    [Test]
    [Arguments("interval=year")]
    [Arguments("interval=day&from=2026-10-10T00:00:00Z&to=2026-10-01T00:00:00Z")]
    public async Task Statistics_InvalidParameters_Return400(string query)
    {
        var response = await _http.GetAsync($"/api/v1/BloodPressure/statistics?{query}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
