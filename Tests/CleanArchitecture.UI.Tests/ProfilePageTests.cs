using System.Net;
using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Pages;
using CleanArchitecture.Presentation.UI.Services;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class ProfilePageTests
{
    private static async Task<(UiHost Host, IRenderedComponent<Profile> Cut)> OpenAsync(ProfileDto? stored = null, string theme = "system", string culture = "de")
    {
        var host = new UiHost(culture);
        if (stored is not null)
        {
            host.Profile.Stored = stored;
        }

        host.Theme.Current = theme;
        await host.SignInAsync();
        var cut = host.Context.Render<Profile>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();
        host.Context.Render<Profile>();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task Loads_StoredValues_AndShowsBmiWithNote()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m));
        using var _ = host;

        await Assert.That(cut.Find("#profile-age").GetAttribute("value")).IsEqualTo("42");
        await Assert.That(cut.Find("#profile-height").GetAttribute("value")).IsEqualTo("180");
        await Assert.That(cut.Find("#profile-weight").GetAttribute("value")).IsEqualTo("82,5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("25,5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("keine Diagnose");
    }

    [Test]
    public async Task BmiIsHidden_WhenAValueIsMissing_AndNeverShowsACategory()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, null));
        using var _ = host;

        await Assert.That(cut.FindAll("#profile-bmi").Count).IsEqualTo(0);

        var (host2, cut2) = await OpenAsync(new ProfileDto(42, 180, 120m));
        using var __ = host2;
        var text = cut2.Find("#profile-bmi").TextContent;
        await Assert.That(text).DoesNotContain("Übergewicht");
        await Assert.That(text).DoesNotContain("Adipositas");
    }

    [Test]
    [Arguments("82,5")]
    [Arguments("82.5")]
    public async Task Save_AcceptsBothDecimalSeparators(string input)
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        cut.Find("#profile-age").Change("42");
        cut.Find("#profile-height").Change("180");
        cut.Find("#profile-weight").Change(input);
        cut.Find("#profile-form").Submit();

        cut.WaitForState(() => cut.FindAll("#profile-saved").Count == 1);
        await Assert.That(host.Profile.Puts[0]).IsEqualTo(new ProfileDto(42, 180, 82.5m));
        await Assert.That(cut.Find("#profile-saved").GetAttribute("role")).IsEqualTo("status");
    }

    [Test]
    public async Task EmptyFields_AreSentAsNull_ToDeleteSingleValues()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m));
        using var _ = host;

        cut.Find("#profile-weight").Change("");
        cut.Find("#profile-form").Submit();

        cut.WaitForState(() => host.Profile.Puts.Count == 1);
        await Assert.That(host.Profile.Puts[0]).IsEqualTo(new ProfileDto(42, 180, null));
    }

    [Test]
    public async Task NonNumericInput_ShowsMessage_WithoutRequest()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        cut.Find("#profile-height").Change("abc");
        cut.Find("#profile-form").Submit();

        await Assert.That(cut.Find("#profile-errors [role=alert]").TextContent).Contains("gültige Zahlen");
        await Assert.That(host.Profile.Puts).IsEmpty();
    }

    [Test]
    public async Task ServerValidation_IsShownInTheErrorRegion_AndKeepsInput()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        host.Profile.PutFailWith = ApiExceptions.Validation("heightCm", "Die Größe muss zwischen 50 und 250 cm liegen.");

        cut.Find("#profile-height").Change("20");
        cut.Find("#profile-form").Submit();

        cut.WaitForState(() => cut.Find("#profile-errors").TextContent.Contains("Die Größe muss"));
        await Assert.That(cut.Find("#profile-height").GetAttribute("value")).IsEqualTo("20");
        host.Context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task Delete_NeedsConfirmation_ThenClearsTheFields()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m));
        using var _ = host;

        cut.Find("#profile-delete").Click();
        await Assert.That(host.Profile.Deletes).IsEqualTo(0);
        cut.Find("#profile-delete-confirm").Click();

        cut.WaitForState(() => host.Profile.Deletes == 1);
        cut.WaitForState(() => cut.Find("#profile-age").GetAttribute("value") is null or "");
        await Assert.That(cut.FindAll("#profile-bmi").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Delete_CanBeCancelled()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m));
        using var _ = host;

        cut.Find("#profile-delete").Click();
        cut.Find("#profile-delete-cancel").Click();

        await Assert.That(host.Profile.Deletes).IsEqualTo(0);
        await Assert.That(cut.FindAll("#profile-delete-confirm").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Theme_ShowsStoredChoice_AndAppliesNewChoiceImmediately()
    {
        var (host, cut) = await OpenAsync(theme: "light");
        using var _ = host;
        await Assert.That(cut.Find("#theme-light").HasAttribute("checked")).IsTrue();

        cut.Find("#theme-dark").Change("dark");

        await Assert.That(host.Theme.Set).IsEquivalentTo(new[] { "dark" });
    }

    [Test]
    public async Task Theme_IsAFieldsetWithLegend_AndNeverSentToTheProfileApi()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        await Assert.That(cut.Find("fieldset legend").TextContent).IsEqualTo("Darstellung");
        cut.Find("#theme-dark").Change("dark");
        await Assert.That(host.Profile.Puts).IsEmpty();
    }

    [Test]
    public async Task EnglishCulture_ShowsEnglishTextAndDecimalPoint()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m), culture: "en");
        using var _ = host;

        await Assert.That(cut.Find("#profile-weight").GetAttribute("value")).IsEqualTo("82.5");
        await Assert.That(cut.Markup).Contains("Appearance");
    }
}

[Category("Unit")]
public class ThemeChoiceTests
{
    [Test]
    [Arguments("light", "light")]
    [Arguments("dark", "dark")]
    [Arguments("system", "system")]
    [Arguments("", "system")]
    [Arguments(null, "system")]
    [Arguments("neon", "system")]
    public async Task Normalize_FallsBackToSystem(string? input, string expected)
        => await Assert.That(ThemeChoice.Normalize(input)).IsEqualTo(expected);
}
