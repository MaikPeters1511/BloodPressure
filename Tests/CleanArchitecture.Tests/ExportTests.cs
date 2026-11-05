using System.Net;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class ExportTests
{
    private ApiFactory _factory = null!;
    private HttpClient _http = null!;

    private static DateTime At(int month, int day) => new(2026, month, day, 8, 0, 0, DateTimeKind.Utc);

    [Before(Test)]
    public void Setup()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClientFor("user-a");
    }

    [After(Test)]
    public async Task Teardown() => await _factory.DisposeAsync();

    private Task AddAsync(DateTime time, int systolic = 120, int diastolic = 80, string user = "user-a")
        => _factory.Repository.AddAsync(new BloodPressure
        {
            UserId = user, Systolisch = systolic, Diastolisch = diastolic, DateTime = time, Pulse = 65, Oxygen = 98
        });

    [Test]
    public async Task Csv_WithTwoMeasurements_HasHeaderAndTwoRows()
    {
        await AddAsync(At(10, 1));
        await AddAsync(At(10, 2), 185, 115);

        var response = await _http.GetAsync("/api/v1/BloodPressure/export?format=csv");
        var lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToArray();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("text/csv");
        await Assert.That(response.Content.Headers.ContentDisposition!.DispositionType).IsEqualTo("attachment");
        await Assert.That(lines.Length).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("Id;DateTimeUtc;Systolisch;Diastolisch;Pulse;Oxygen;Category");
        await Assert.That(lines[1]).IsEqualTo("1;2026-10-01T08:00:00Z;120;80;65;98;Elevated");
        await Assert.That(lines[2]).IsEqualTo("2;2026-10-02T08:00:00Z;185;115;65;98;Hypertension");
    }

    [Test]
    public async Task Csv_DoesNotContainOtherUsersData()
    {
        await AddAsync(At(10, 1));
        await AddAsync(At(10, 2), 190, 120, user: "user-b");

        var text = await _http.GetStringAsync("/api/v1/BloodPressure/export?format=csv");

        await Assert.That(text).DoesNotContain("190;120");
        await Assert.That(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length).IsEqualTo(2);
    }

    [Test]
    public async Task Csv_RespectsTimeRange()
    {
        await AddAsync(At(10, 1));
        await AddAsync(At(10, 20));

        var text = await _http.GetStringAsync("/api/v1/BloodPressure/export?format=csv&from=2026-10-10T00:00:00Z&to=2026-10-30T00:00:00Z");

        await Assert.That(text).Contains("2026-10-20T08:00:00Z");
        await Assert.That(text).DoesNotContain("2026-10-01T08:00:00Z");
    }

    [Test]
    public async Task Pdf_StartsWithPdfMarkerAndHasPdfContentType()
    {
        await AddAsync(At(10, 1));

        var response = await _http.GetAsync("/api/v1/BloodPressure/export?format=pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("application/pdf");
        await Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task Pdf_WithoutMeasurements_IsStillProduced()
    {
        var response = await _http.GetAsync("/api/v1/BloodPressure/export?format=pdf");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await response.Content.ReadAsByteArrayAsync()).Length).IsGreaterThan(500);
    }

    [Test]
    public async Task Export_WithMoreThan10000Measurements_Returns400()
    {
        for (var i = 0; i < 10_001; i++)
        {
            await AddAsync(At(1, 1).AddMinutes(i));
        }

        var response = await _http.GetAsync("/api/v1/BloodPressure/export?format=csv");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Zeitraum");
    }

    [Test]
    public async Task Export_With10000Measurements_Succeeds()
    {
        for (var i = 0; i < 10_000; i++)
        {
            await AddAsync(At(1, 1).AddMinutes(i));
        }

        var response = await _http.GetAsync("/api/v1/BloodPressure/export?format=csv");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("format=xml")]
    [Arguments("format=csv&from=2026-10-10T00:00:00Z&to=2026-10-01T00:00:00Z")]
    public async Task Export_InvalidParameters_Return400(string query)
    {
        var response = await _http.GetAsync($"/api/v1/BloodPressure/export?{query}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Export_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/BloodPressure/export?format=csv");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
