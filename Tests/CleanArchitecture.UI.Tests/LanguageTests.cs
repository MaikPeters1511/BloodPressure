using System.Globalization;
using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Layout;
using CleanArchitecture.Presentation.UI.Pages;
using CleanArchitecture.Presentation.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class LanguageChoiceTests
{
    [Test]
    [Arguments("system", "en-US", "en")]
    [Arguments("system", "en", "en")]
    [Arguments("system", "de-DE", "de")]
    [Arguments("system", "fr", "de")]
    [Arguments("system", null, "de")]
    [Arguments(null, "en-GB", "en")]
    [Arguments("de", "en-US", "de")]
    [Arguments("en", "de-DE", "en")]
    [Arguments("klingon", "fr", "de")]
    public async Task Resolve_UsesTheChoice_ElseTheBrowserLanguage_ElseGerman(string? choice, string? browser, string expected)
        => await Assert.That(LanguageChoice.Resolve(choice, browser)).IsEqualTo(expected);

    [Test]
    [Arguments(null, "system")]
    [Arguments("", "system")]
    [Arguments("fr", "system")]
    [Arguments("de", "de")]
    [Arguments("en", "en")]
    public async Task Normalize_UnknownValues_MeanSystem(string? value, string expected)
        => await Assert.That(LanguageChoice.Normalize(value)).IsEqualTo(expected);
}

/// <summary>
/// Der echte Dienst: Kultur, lang-Attribut, Speicher über JavaScript. Die Standardkultur ist prozessweit; damit andere Tests
/// sie nicht sehen (und sich die Tests nicht gegenseitig stören), laufen diese Tests nacheinander und allein.
/// </summary>
[Category("Unit")]
[NotInParallel]
public class LanguageServiceTests
{
    private static (BunitContext Context, LanguageService Service) Create(string? stored, string? browser)
    {
        var context = new BunitContext();
        context.JSInterop.Setup<string?>("bpLang.get").SetResult(stored);
        context.JSInterop.Setup<string?>("getBrowserLanguage").SetResult(browser);
        context.JSInterop.SetupVoid("bpLang.set", _ => true).SetVoidResult();
        return (context, new LanguageService(context.JSInterop.JSRuntime));
    }

    private static async Task WithCultureRestoredAsync(Func<Task> test)
    {
        var (defaultCulture, defaultUi) = (CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
        try
        {
            await test();
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUi;
        }
    }

    [Test]
    public async Task Initialize_UsesTheStoredChoice_BeforeTheBrowserLanguage() => await WithCultureRestoredAsync(async () =>
    {
        var (context, service) = Create(stored: "en", browser: "de-DE");
        using var _ = context;
        var changes = 0;
        service.Changed += () => changes++;

        await service.InitializeAsync();

        await Assert.That(service.Current).IsEqualTo("en");
        await Assert.That(CultureInfo.DefaultThreadCurrentUICulture?.Name).IsEqualTo("en");
        await Assert.That(CultureInfo.DefaultThreadCurrentCulture?.Name).IsEqualTo("en");
        await Assert.That(changes).IsEqualTo(0);
        context.JSInterop.VerifyInvoke("bpLang.set");
    });

    [Test]
    public async Task Initialize_WithoutChoice_FollowsTheBrowser() => await WithCultureRestoredAsync(async () =>
    {
        var (context, service) = Create(stored: null, browser: "fr-FR");
        using var _ = context;

        await service.InitializeAsync();

        await Assert.That(service.Current).IsEqualTo("de");
        await Assert.That(CultureInfo.DefaultThreadCurrentUICulture?.Name).IsEqualTo("de");
    });

    [Test]
    public async Task SetChoice_AppliesAtOnce_StoresTheChoice_AndNotifies() => await WithCultureRestoredAsync(async () =>
    {
        var (context, service) = Create(stored: null, browser: "de-DE");
        using var _ = context;
        await service.InitializeAsync();
        var changes = 0;
        service.Changed += () => changes++;

        await service.SetChoiceAsync("en");

        await Assert.That(service.Current).IsEqualTo("en");
        await Assert.That(CultureInfo.DefaultThreadCurrentUICulture?.Name).IsEqualTo("en");
        await Assert.That(changes).IsEqualTo(1);
        var call = context.JSInterop.Invocations["bpLang.set"].Last();
        await Assert.That(call.Arguments[0]).IsEqualTo("en");
        await Assert.That(call.Arguments[1]).IsEqualTo("en");
    });

    [Test]
    public async Task SetChoice_System_RemovesTheStoredChoice_AndFollowsTheDevice() => await WithCultureRestoredAsync(async () =>
    {
        var (context, service) = Create(stored: "en", browser: "de-DE");
        using var _ = context;
        await service.InitializeAsync();

        await service.SetChoiceAsync("system");

        await Assert.That(service.Current).IsEqualTo("de");
        var call = context.JSInterop.Invocations["bpLang.set"].Last();
        await Assert.That(call.Arguments[0]).IsEqualTo("system");
        await Assert.That(call.Arguments[1]).IsEqualTo("de");
    });

    [Test]
    public async Task SetChoice_NeverSendsAnythingToTheServer()
    {
        // Der Dienst kennt nur die JavaScript-Schnittstelle des Browsers (kein HttpClient, keine API).
        var parameters = typeof(LanguageService).GetConstructors().Single().GetParameters();
        await Assert.That(parameters.Select(p => p.ParameterType.Name)).IsEquivalentTo(["IJSRuntime"]);
    }
}

/// <summary>Der echte Lokalisierer folgt dem Sprachdienst, nicht der Thread-Kultur (Blazor WebAssembly legt sie beim Start fest).</summary>
[Category("Unit")]
public class LanguageLocalizerTests
{
    [Test]
    public async Task FollowsTheLanguageService_AtOnce()
    {
        var language = new FakeLanguage("de");
        var localizer = new LanguageLocalizer(language);
        await Assert.That(localizer["Nav_Measurements"].Value).IsEqualTo("Messwerte");
        await Assert.That(localizer["Language_Changed", "English"].Value).IsEqualTo("Sprache: English");

        await language.SetChoiceAsync("en");

        await Assert.That(localizer["Nav_Measurements"].Value).IsEqualTo("Measurements");
        await Assert.That(localizer["Language_Changed", "English"].Value).IsEqualTo("Language: English");
        await Assert.That(localizer["Culture_Name"].Value).IsEqualTo("en");
    }

    [Test]
    public async Task UnknownKey_ReturnsTheKey_AndMarksItAsNotFound()
    {
        var value = new LanguageLocalizer(new FakeLanguage("en"))["Does_Not_Exist"];

        await Assert.That(value.Value).IsEqualTo("Does_Not_Exist");
        await Assert.That(value.ResourceNotFound).IsTrue();
    }
}

[Category("Unit")]
public class LanguageSwitchPageTests
{
    private static async Task<(UiHost Host, IRenderedComponent<Profile> Cut)> OpenAsync(ProfileDto? stored = null, string culture = "de", string choice = "system")
    {
        var host = new UiHost(culture);
        host.Language.Choice = choice;
        if (stored is not null)
        {
            host.Profile.Stored = stored;
        }

        await host.SignInAsync();
        var cut = host.Context.Render<Profile>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task ShowsTheLanguageGroup_WithThreeOptions_AndTheStoredChoice()
    {
        var (host, cut) = await OpenAsync(choice: "en");
        using var _ = host;

        await Assert.That(cut.Find("#language-group legend").TextContent).IsEqualTo("Sprache");
        var radios = cut.FindAll("#language-group input[type=radio]");
        await Assert.That(radios.Select(r => r.GetAttribute("value"))).IsEquivalentTo(["system", "de", "en"]);
        await Assert.That(cut.Find("#language-en").HasAttribute("checked")).IsTrue();
        await Assert.That(cut.Find("#language-de").HasAttribute("checked")).IsFalse();
        // Die Eigennamen tragen einen Sprachhinweis für Screenreader.
        await Assert.That(cut.Find("label[for=language-de]").GetAttribute("lang")).IsEqualTo("de");
        await Assert.That(cut.Find("label[for=language-en]").GetAttribute("lang")).IsEqualTo("en");
        await Assert.That(cut.Find("label[for=language-system]").HasAttribute("lang")).IsFalse();
    }

    [Test]
    public async Task ChoosingEnglish_SwitchesTheTexts_AtOnce_AndAnnouncesIt_WithoutTouchingTheApi()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        await Assert.That(cut.Find("h1").TextContent).IsEqualTo("Profil & Einstellungen");

        cut.Find("#language-en").Change("en");

        cut.WaitForState(() => cut.Find("h1").TextContent != "Profil & Einstellungen");
        await Assert.That(host.Language.Set).IsEquivalentTo(["en"]);
        await Assert.That(cut.Find("#language-group legend").TextContent).IsEqualTo("Language");
        await Assert.That(cut.Find("#profile-save").TextContent).IsEqualTo("Save");
        var status = cut.Find("#profile-saved");
        await Assert.That(status.GetAttribute("role")).IsEqualTo("status");
        await Assert.That(status.TextContent).IsEqualTo("Language: English");
        await Assert.That(host.Profile.Puts).IsEmpty();
        await Assert.That(host.Profile.Deletes).IsEqualTo(0);
    }

    [Test]
    public async Task ChoosingSystem_FollowsTheDevice()
    {
        var (host, cut) = await OpenAsync(culture: "en", choice: "en");
        using var _ = host;

        cut.Find("#language-system").Change("system");

        cut.WaitForState(() => cut.Find("#language-group legend").TextContent == "Sprache");
        await Assert.That(cut.Find("#profile-saved").TextContent).IsEqualTo("Sprache: System");
    }

    [Test]
    public async Task NumberFormats_FollowTheLanguage_InBothDirections()
    {
        var (host, cut) = await OpenAsync(new ProfileDto(42, 180, 82.5m));
        using var _ = host;
        await Assert.That(cut.Find("#profile-weight").GetAttribute("value")).IsEqualTo("82,5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("25,5");

        cut.Find("#language-en").Change("en");
        cut.WaitForState(() => cut.Find("#profile-weight").GetAttribute("value") == "82.5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("25.5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("not a diagnosis");

        cut.Find("#language-de").Change("de");
        cut.WaitForState(() => cut.Find("#profile-weight").GetAttribute("value") == "82,5");
        await Assert.That(cut.Find("#profile-bmi").TextContent).Contains("25,5");
    }

    [Test]
    public async Task UnsavedInput_IsKept_WhenTheLanguageChanges()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        cut.Find("#profile-age").Change("42");
        cut.Find("#profile-height").Change("180");
        cut.Find("#profile-weight").Change("82,5");

        cut.Find("#language-en").Change("en");

        cut.WaitForState(() => cut.Find("#profile-weight").GetAttribute("value") == "82.5");
        await Assert.That(cut.Find("#profile-age").GetAttribute("value")).IsEqualTo("42");
        await Assert.That(cut.Find("#profile-height").GetAttribute("value")).IsEqualTo("180");
        await Assert.That(host.Profile.Puts).IsEmpty();
    }

    [Test]
    public async Task BothDecimalSeparators_StayValid_AfterTheSwitch()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;
        cut.Find("#language-en").Change("en");
        cut.WaitForState(() => cut.Find("#language-group legend").TextContent == "Language");

        cut.Find("#profile-age").Change("42");
        cut.Find("#profile-height").Change("180");
        cut.Find("#profile-weight").Change("82,5");
        cut.Find("#profile-form").Submit();

        cut.WaitForState(() => host.Profile.Puts.Count == 1);
        await Assert.That(host.Profile.Puts[0]).IsEqualTo(new ProfileDto(42, 180, 82.5m));
    }
}

/// <summary>Die Navigation (und damit jede Seite hinter dem Layout) wird beim Sprachwechsel neu aufgebaut.</summary>
[Category("Unit")]
public class LanguageSwitchLayoutTests
{
    [Test]
    public async Task Navigation_IsRebuilt_InTheNewLanguage()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var cut = host.Context.Render<MainLayout>(p => p.Add(x => x.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<p id='body'>x</p>"))));
        await Assert.That(cut.Markup).Contains("Messwerte");

        await host.Language.SetChoiceAsync("en");

        cut.WaitForState(() => cut.Markup.Contains("Measurements"));
        await Assert.That(cut.Markup).DoesNotContain("Messwerte");
        await Assert.That(cut.FindAll("#body").Count).IsEqualTo(1);
    }
}
