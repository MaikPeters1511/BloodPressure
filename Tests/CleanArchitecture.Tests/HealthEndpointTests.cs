using System.Net;

namespace CleanArchitecture.Tests;

[Category("Integration")]
public class HealthEndpointTests
{
    [Test]
    public async Task Alive_IsAvailableOutsideDevelopment_WithoutToken()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/alive");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("Healthy");
    }

    [Test]
    public async Task FullHealth_IsNotExposedOutsideDevelopment()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/health");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
