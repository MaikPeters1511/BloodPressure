using System.Net;
using Bunit;
using CleanArchitecture.Presentation.UI.Pages;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class MeasurementsPageTests
{
    private static async Task<(UiHost Host, IRenderedComponent<Measurements> Cut)> OpenAsync(int items = 0, string culture = "de")
    {
        var host = new UiHost(culture);
        host.Api.Seed(items);
        await host.SignInAsync();
        var cut = host.Context.Render<Measurements>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();

        host.Context.Render<Measurements>();

        await Assert.That(host.CurrentPath).IsEqualTo("/login");
        await Assert.That(host.Api.ListCalls).IsEmpty();
    }

    [Test]
    public async Task TwelveMeasurements_ShowFirstPageWithTenRows()
    {
        var (host, cut) = await OpenAsync(12);
        using var _ = host;

        await Assert.That(cut.FindAll("tbody tr").Count).IsEqualTo(10);
        await Assert.That(cut.Find("#page-indicator").TextContent).IsEqualTo("Seite 1 von 2");
        await Assert.That(cut.Find("#prev-page").HasAttribute("disabled")).IsTrue();
        await Assert.That(cut.Find("#next-page").HasAttribute("disabled")).IsFalse();
        await Assert.That(host.Api.ListCalls[0]).IsEqualTo((1, 10, "-dateTime"));
    }

    [Test]
    public async Task NextPage_LoadsSecondPage()
    {
        var (host, cut) = await OpenAsync(12);
        using var _ = host;

        cut.Find("#next-page").Click();

        cut.WaitForState(() => cut.FindAll("tbody tr").Count == 2);
        await Assert.That(cut.Find("#page-indicator").TextContent).IsEqualTo("Seite 2 von 2");
        await Assert.That(cut.Find("#next-page").HasAttribute("disabled")).IsTrue();
        await Assert.That(host.Api.ListCalls.Last().Page).IsEqualTo(2);
    }

    [Test]
    public async Task NoMeasurements_ShowsHint_AndNoTable()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;

        await Assert.That(cut.Markup).Contains("Noch keine Messwerte vorhanden.");
        await Assert.That(cut.FindAll("tbody tr")).IsEmpty();
    }

    [Test]
    public async Task Table_HasCaptionAndLocalizedCategory_AndDisclaimer()
    {
        var host = new UiHost();
        using var _ = host;
        host.Api.Items.Add(FakeBloodPressureApi.Make(1, 150, 95, category: "Grade1"));
        await host.SignInAsync();

        var cut = host.Context.Render<Measurements>();

        cut.WaitForAssertion(() => cut.Find("caption"));
        await Assert.That(cut.Find("caption").TextContent).IsEqualTo("Meine Blutdruckmesswerte");
        await Assert.That(cut.Find("tbody tr").TextContent).Contains("Hypertonie Grad 1");
        await Assert.That(cut.Markup).Contains("keine Diagnose");
        await Assert.That(cut.FindAll("th[scope=col]").Count).IsGreaterThan(4);
    }

    [Test]
    public async Task EnglishCulture_UsesEnglishTexts()
    {
        var (host, cut) = await OpenAsync(12, "en");
        using var _ = host;

        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("Measurements");
        await Assert.That(cut.Find("#page-indicator").TextContent).IsEqualTo("Page 1 of 2");
    }

    [Test]
    public async Task Unauthorized_SignsOutAndRedirectsToLogin()
    {
        var host = new UiHost();
        using var _ = host;
        host.Api.FailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);
        await host.SignInAsync();

        host.Context.Render<Measurements>();

        await Assert.That(host.Session.IsAuthenticated).IsFalse();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task ServerError_ShowsGenericAlert_WithoutDetails()
    {
        var host = new UiHost();
        using var _ = host;
        host.Api.FailWith = new HttpRequestException("Npgsql: password authentication failed");
        await host.SignInAsync();

        var cut = host.Context.Render<Measurements>();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("Etwas ist schiefgegangen");
        await Assert.That(cut.Markup).DoesNotContain("Npgsql");
    }

    [Test]
    public async Task EveryFormInput_HasALabelBoundByFor()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;

        var inputs = cut.FindAll("#create-form input");
        await Assert.That(inputs.Count).IsEqualTo(5);
        foreach (var input in inputs)
        {
            var id = input.GetAttribute("id");
            await Assert.That(cut.FindAll($"label[for={id}]").Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task CreateForm_DefaultsTimeToNow()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;

        await Assert.That(cut.Find("#measured-at").GetAttribute("value")).IsEqualTo("2026-11-04T12:00");
    }

    [Test]
    public async Task Create_WithValidValues_CallsApiAndReloadsList()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;
        cut.Find("#systolisch").Change("125");
        cut.Find("#diastolisch").Change("82");
        cut.Find("#pulse").Change("66");
        cut.Find("#oxygen").Change("97");
        cut.Find("#measured-at").Change("2026-10-02T08:30");

        cut.Find("#create-form").Submit();

        cut.WaitForState(() => cut.FindAll("tbody tr").Count == 1);
        var created = host.Api.Created.Single();
        await Assert.That(created.Systolisch).IsEqualTo(125);
        await Assert.That(created.Diastolisch).IsEqualTo(82);
        await Assert.That(created.Pulse).IsEqualTo(66);
        await Assert.That(created.Oxygen).IsEqualTo(97);
        await Assert.That(created.DateTime).IsEqualTo(new DateTime(2026, 10, 2, 8, 30, 0, DateTimeKind.Utc));
        await Assert.That(host.Api.ListCalls.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Create_WithInvalidValues_ShowsApiErrors_AndKeepsInput()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;
        host.Api.CreateFailWith = ApiExceptions.Validation("systolisch", "Systolisch muss größer als Diastolisch sein.");
        cut.Find("#systolisch").Change("70");
        cut.Find("#diastolisch").Change("90");
        cut.Find("#pulse").Change("66");
        cut.Find("#oxygen").Change("97");

        cut.Find("#create-form").Submit();

        cut.WaitForAssertion(() => cut.Find("#create-form [role=alert]"));
        await Assert.That(cut.Find("#create-form [role=alert]").TextContent).Contains("Systolisch muss größer als Diastolisch sein.");
        await Assert.That(cut.Find("#systolisch").GetAttribute("value")).IsEqualTo("70");
        await Assert.That(host.Api.Created).IsEmpty();
    }

    [Test]
    public async Task Create_WithMissingFields_ShowsHint_WithoutCallingTheApi()
    {
        var (host, cut) = await OpenAsync(0);
        using var _ = host;
        cut.Find("#systolisch").Change("120");

        cut.Find("#create-form").Submit();

        cut.WaitForAssertion(() => cut.Find("#create-form [role=alert]"));
        await Assert.That(cut.Find("#create-form [role=alert]").TextContent).Contains("Bitte alle Felder ausfüllen");
        await Assert.That(host.Api.Created).IsEmpty();
    }

    [Test]
    public async Task Delete_AsksForConfirmation_ThenDeletesWithIfMatch_AndReloads()
    {
        var (host, cut) = await OpenAsync(2);
        using var _ = host;
        var buttons = cut.FindAll(".delete-button");
        await Assert.That(buttons[0].GetAttribute("aria-label")).Contains("Messwert");

        buttons[0].Click();
        await Assert.That(host.Api.Deleted).IsEmpty();
        cut.Find(".confirm-delete").Click();

        cut.WaitForState(() => cut.FindAll("tbody tr").Count == 1);
        var deleted = host.Api.Deleted.Single();
        await Assert.That(deleted.IfMatch).IsEqualTo("\"7\"");
        await Assert.That(host.Api.ListCalls.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Delete_CanBeCancelled()
    {
        var (host, cut) = await OpenAsync(1);
        using var _ = host;

        cut.Find(".delete-button").Click();
        cut.Find(".cancel-delete").Click();

        await Assert.That(host.Api.Deleted).IsEmpty();
        await Assert.That(cut.FindAll(".confirm-delete")).IsEmpty();
    }

    [Test]
    public async Task Delete_WithStaleVersion_ShowsConflictMessage_AndReloads()
    {
        var (host, cut) = await OpenAsync(1);
        using var _ = host;
        host.Api.DeleteFailWith = ApiExceptions.Create(HttpStatusCode.PreconditionFailed);

        cut.Find(".delete-button").Click();
        cut.Find(".confirm-delete").Click();

        cut.WaitForAssertion(() => cut.Find("[role=alert]"));
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("zwischenzeitlich geändert");
        await Assert.That(host.Api.ListCalls.Count).IsEqualTo(2);
    }
}
