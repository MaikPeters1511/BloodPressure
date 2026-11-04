using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class ValidationTests
{
    private static readonly DateTime Past = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    private ApiFactory _factory = null!;
    private HttpClient _http = null!;

    [Before(Test)]
    public void Setup()
    {
        _factory = new ApiFactory();
        _http = _factory.CreateClientFor("user-a");
    }

    [After(Test)]
    public async Task Teardown() => await _factory.DisposeAsync();

    private static object Body(int systolisch = 120, int diastolisch = 80, DateTime? dateTime = null, int pulse = 65, int oxygen = 98)
        => new { systolisch, diastolisch, dateTime = dateTime ?? Past, pulse, oxygen };

    private static async Task<HashSet<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    [Test]
    public async Task Create_WithSeveralInvalidFields_ReportsAllFields()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(systolisch: 999, oxygen: 150));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var fields = await ErrorFieldsAsync(response);
        await Assert.That(fields).Contains("systolisch");
        await Assert.That(fields).Contains("oxygen");
    }

    [Test]
    public async Task Create_SystolischNotAboveDiastolisch_ReportsSystolisch()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(systolisch: 70, diastolisch: 90));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorFieldsAsync(response)).Contains("systolisch");
    }

    [Test]
    public async Task Create_SystolischEqualsDiastolisch_IsRejected()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(systolisch: 90, diastolisch: 90));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_DateTimeInFuture_ReportsDateTime()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(dateTime: DateTime.UtcNow.AddHours(1)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorFieldsAsync(response)).Contains("dateTime");
    }

    [Test]
    public async Task Create_DateTimeSlightlyAhead_IsAccepted()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(dateTime: DateTime.UtcNow.AddMinutes(2)));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
    }

    [Test]
    public async Task Update_WithInvalidFields_ReportsFields()
    {
        var created = await _http.PostAsJsonAsync("/api/BloodPressure", Body());
        var location = created.Headers.Location!;

        var response = await _http.PutAsJsonAsync(location, Body(systolisch: 70, diastolisch: 90));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ErrorFieldsAsync(response)).Contains("systolisch");
    }

    [Test]
    public async Task InvalidRequest_DoesNotLogErrors()
    {
        await _http.PostAsJsonAsync("/api/BloodPressure", Body(systolisch: 999));

        var errors = _factory.Logs.Entries.Where(e => e.Level >= LogLevel.Error).ToList();
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task InvalidRequest_DoesNotExposeInternalDetails()
    {
        var response = await _http.PostAsJsonAsync("/api/BloodPressure", Body(systolisch: 999));

        var text = await response.Content.ReadAsStringAsync();
        await Assert.That(text).DoesNotContain("   at ");
        await Assert.That(text).DoesNotContain("Exception");
    }
}
