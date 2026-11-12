using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using CleanArchitecture.Presentation.UI.Pages;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class ConfirmEmailPageTests
{
    private static IRenderedComponent<ConfirmEmail> Open(UiHost host, string query)
    {
        host.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/confirm-email{query}");
        return host.Context.Render<ConfirmEmail>();
    }

    [Test]
    public async Task ValidLink_ConfirmsOnce_AndShowsSuccessWithLoginLink()
    {
        using var host = new UiHost();

        var cut = Open(host, "?userId=u1&code=abc%2Fdef");

        cut.WaitForAssertion(() => cut.Find("[role=status]"));
        await Assert.That(cut.Find("[role=status]").TextContent).Contains("E-Mail bestätigt");
        await Assert.That(cut.Find("a[href$=login]").TextContent).Contains("Zur Anmeldung");
        await Assert.That(host.Auth.Confirmed).IsEquivalentTo(new[] { ("u1", "abc/def") });
        await Assert.That(cut.FindAll("[role=alert]").Count).IsEqualTo(0);
    }

    [Test]
    public async Task InvalidCode_ShowsExpiredMessage_WithLoginLink()
    {
        using var host = new UiHost();
        host.Auth.OnConfirmEmail = (_, _) => throw ApiExceptions.Create(HttpStatusCode.Unauthorized);

        var cut = Open(host, "?userId=u1&code=bad");

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Der Link ist ungültig oder abgelaufen");
        await Assert.That(cut.Find("a[href$=login]")).IsNotNull();
        await Assert.That(cut.FindAll("[role=status]").Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("")]
    [Arguments("?userId=u1")]
    [Arguments("?code=abc")]
    public async Task MissingParameters_ShowError_WithoutApiCall(string query)
    {
        using var host = new UiHost();

        var cut = Open(host, query);

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Der Link ist ungültig oder abgelaufen");
        await Assert.That(host.Auth.Calls).IsEmpty();
    }

    [Test]
    public async Task UnexpectedError_ShowsGenericMessage_WithoutDetails()
    {
        using var host = new UiHost();
        host.Auth.OnConfirmEmail = (_, _) => throw new HttpRequestException("secret host");

        var cut = Open(host, "?userId=u1&code=abc");

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Etwas ist schiefgegangen");
        await Assert.That(cut.Markup).DoesNotContain("secret");
    }

    [Test]
    public async Task Page_HasHeading_AndNeedsNoSignIn()
    {
        using var host = new UiHost();

        var cut = Open(host, "?userId=u1&code=abc");

        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("E-Mail-Bestätigung");
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
        await Assert.That(host.CurrentPath).IsEqualTo("/confirm-email");
    }

    [Test]
    public async Task EnglishCulture_RendersEnglishTexts()
    {
        using var host = new UiHost("en");

        var cut = Open(host, "?userId=u1&code=abc");

        cut.WaitForAssertion(() => cut.Find("[role=status]"));
        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("Email confirmation");
        await Assert.That(cut.Find("[role=status]").TextContent).Contains("Email confirmed");
    }
}
