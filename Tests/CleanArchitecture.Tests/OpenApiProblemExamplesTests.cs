using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class OpenApiProblemExamplesTests
{
    private sealed class MapOpenApiFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapOpenApi());
        };
    }

    private static async Task<JsonElement> LoadDocumentAsync()
    {
        await using var factory = new ApiFactory();
        using var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddTransient<IStartupFilter, MapOpenApiFilter>()))
            .CreateClient();

        var json = await client.GetStringAsync("/openapi/v1.json");
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Test]
    [Arguments("/api/v1/BloodPressure", "post", "400")]
    [Arguments("/api/v1/BloodPressure", "get", "401")]
    [Arguments("/api/v1/BloodPressure/{id}", "get", "404")]
    [Arguments("/api/v1/BloodPressure/{id}", "put", "412")]
    [Arguments("/api/v1/BloodPressure", "get", "429")]
    public async Task Operation_DocumentsProblemExample(string path, string method, string status)
    {
        var doc = await LoadDocumentAsync();

        var response = doc.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").GetProperty(status);
        var example = response.GetProperty("content").GetProperty("application/problem+json").GetProperty("example");

        await Assert.That(example.GetProperty("status").GetInt32()).IsEqualTo(int.Parse(status));
    }

    [Test]
    public async Task ListOperation_DocumentsPagingParametersAndEnvelope()
    {
        var doc = await LoadDocumentAsync();

        var op = doc.GetProperty("paths").GetProperty("/api/v1/BloodPressure").GetProperty("get");
        var parameters = op.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()!.ToLowerInvariant()).ToList();
        var schema = op.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").ToString();

        await Assert.That(parameters).Contains("page");
        await Assert.That(parameters).Contains("pagesize");
        await Assert.That(schema).Contains("PagedResult");
    }

    [Test]
    [Arguments("/api/v1/BloodPressure", "get")]
    [Arguments("/api/v1/BloodPressure/statistics", "get")]
    [Arguments("/api/v1/BloodPressure/{id}", "get")]
    public async Task ClassifyingOperation_HasDisclaimer(string path, string method)
    {
        var doc = await LoadDocumentAsync();

        var description = doc.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("description").GetString();

        await Assert.That(description).Contains("keine Diagnose");
    }
}
