using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class ApiEndpointTests
{
    [Test]
    public async Task Debug_AllowsHttp()
        => await Assert.That(ApiEndpoint.Resolve("http://localhost:5000", allowCleartext: true)).IsEqualTo(new Uri("http://localhost:5000"));

    [Test]
    public async Task Release_AcceptsHttps()
        => await Assert.That(ApiEndpoint.Resolve("https://api.example.test", allowCleartext: false)).IsEqualTo(new Uri("https://api.example.test"));

    [Test]
    public async Task Release_RejectsHttp_WithClearMessage()
    {
        var ex = await Assert.That(() => ApiEndpoint.Resolve("http://api.example.test", allowCleartext: false)).Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).Contains("HTTPS");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("not a url")]
    [Arguments("ftp://host")]
    public async Task Missing_OrInvalid_Throws(string? value)
        => await Assert.That(() => ApiEndpoint.Resolve(value, allowCleartext: true)).Throws<InvalidOperationException>();

    [Test]
    public async Task AndroidEmulator_RewritesLocalhostHost()
        => await Assert.That(ApiEndpoint.ForAndroidEmulator(new Uri("http://localhost:5000"))).IsEqualTo(new Uri("http://10.0.2.2:5000"));
}
