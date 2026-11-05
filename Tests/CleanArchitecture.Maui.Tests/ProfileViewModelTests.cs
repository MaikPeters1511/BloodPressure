using System.Globalization;
using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class LanguageSettingsTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly FakeLanguageApplier _applier = new();

    private LanguageSettings Create(CultureInfo device, out ResxLocalizer localizer)
    {
        localizer = new ResxLocalizer(LanguageSettings.ResolveStored(_store, device));
        return new LanguageSettings(_store, localizer, _applier, device);
    }

    [Test]
    public async Task Default_IsSystem()
        => await Assert.That(Create(Culture.De, out _).Current).IsEqualTo("system");

    [Test]
    [Arguments("en-US", "en")]
    [Arguments("en-GB", "en")]
    [Arguments("de-DE", "de")]
    [Arguments("fr-FR", "de")]
    public async Task WithoutChoice_TheDeviceLanguageDecides_EnglishOtherwiseGerman(string device, string expected)
    {
        Create(new CultureInfo(device), out var localizer);

        await Assert.That(localizer.Culture.Name).IsEqualTo(expected);
    }

    [Test]
    public async Task Set_AppliesImmediately_SwitchesTheTexts_AndStoresLocally()
    {
        var settings = Create(Culture.De, out var localizer);
        await Assert.That(localizer["Tab_Measurements"]).IsEqualTo("Messwerte");

        settings.Set("en");

        await Assert.That(_applier.Applied.Select(c => c.Name)).IsEquivalentTo(new[] { "en" });
        await Assert.That(_store.Items["bp.language"]).IsEqualTo("en");
        await Assert.That(settings.Current).IsEqualTo("en");
        await Assert.That(localizer.Culture.Name).IsEqualTo("en");
        await Assert.That(localizer["Tab_Measurements"]).IsEqualTo("Readings");
    }

    [Test]
    public async Task ChoiceWinsOverTheDeviceLanguage_AfterRestart()
    {
        _store.Items["bp.language"] = "de";

        var settings = Create(Culture.En, out var localizer);

        await Assert.That(settings.Current).IsEqualTo("de");
        await Assert.That(localizer.Culture.Name).IsEqualTo("de");
    }

    [Test]
    public async Task System_RemovesTheStoredChoice_AndFollowsTheDevice()
    {
        _store.Items["bp.language"] = "en";
        var settings = Create(Culture.De, out var localizer);

        settings.Set("system");

        await Assert.That(_store.Items.ContainsKey("bp.language")).IsFalse();
        await Assert.That(localizer.Culture.Name).IsEqualTo("de");
        await Assert.That(_applier.Applied.Single().Name).IsEqualTo("de");
    }

    [Test]
    public async Task UnknownValues_AreTreatedAsSystem()
    {
        _store.Items["bp.language"] = "klingon";

        await Assert.That(Create(Culture.De, out _).Current).IsEqualTo("system");
    }
}

[Category("Unit")]
public class ThemeSettingsTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly FakeThemeApplier _applier = new();

    [Test]
    public async Task Default_IsSystem()
        => await Assert.That(new ThemeSettings(_store, _applier).Current).IsEqualTo("system");

    [Test]
    public async Task Set_AppliesImmediately_AndStoresLocally()
    {
        var settings = new ThemeSettings(_store, _applier);

        settings.Set("dark");

        await Assert.That(_applier.Applied).IsEquivalentTo(new[] { "dark" });
        await Assert.That(_store.Items["bp.theme"]).IsEqualTo("dark");
        await Assert.That(settings.Current).IsEqualTo("dark");
    }

    [Test]
    public async Task ApplyStored_RestoresTheChoiceAfterRestart()
    {
        _store.Items["bp.theme"] = "light";

        var settings = new ThemeSettings(_store, _applier);
        settings.ApplyStored();

        await Assert.That(settings.Current).IsEqualTo("light");
        await Assert.That(_applier.Applied).IsEquivalentTo(new[] { "light" });
    }

    [Test]
    public async Task Set_System_RemovesTheStoredValue()
    {
        var settings = new ThemeSettings(_store, _applier);
        settings.Set("dark");

        settings.Set("system");

        await Assert.That(_store.Items.ContainsKey("bp.theme")).IsFalse();
        await Assert.That(_applier.Applied.Last()).IsEqualTo("system");
    }

    [Test]
    [Arguments("neon")]
    [Arguments("")]
    public async Task UnknownStoredValue_FallsBackToSystem(string stored)
    {
        _store.Items["bp.theme"] = stored;

        await Assert.That(new ThemeSettings(_store, _applier).Current).IsEqualTo("system");
    }
}

[Category("Unit")]
public class ProfileViewModelTests
{
    private readonly FakeSecureStore _secure = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeProfileApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeThemeApplier _applier = new();
    private readonly FakeLanguageApplier _languageApplier = new();
    private ResxLocalizer _localizer = null!;
    private SessionService _session = null!;

    private async Task<ProfileViewModel> CreateAsync(ProfileDto? stored = null, bool signedIn = true, string culture = "de")
    {
        if (stored is not null)
        {
            _api.Stored = stored;
        }

        _session = new SessionService(_secure, _auth, Clock.Fixed());
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        var device = culture == "de" ? Culture.De : Culture.En;
        _localizer = new ResxLocalizer(LanguageSettings.ResolveStored(_settings, device));
        return new ProfileViewModel(_api, _session, _nav, _dialogs, _localizer,
            new ThemeSettings(_settings, _applier), new LanguageSettings(_settings, _localizer, _languageApplier, device));
    }

    [Test]
    public async Task NotSignedIn_NavigatesToLogin()
    {
        var vm = await CreateAsync(signedIn: false);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
    }

    [Test]
    public async Task Loads_StoredValues_AndBmiWithNote()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, 82.5m));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.AgeText).IsEqualTo("42");
        await Assert.That(vm.HeightText).IsEqualTo("180");
        await Assert.That(vm.WeightText).IsEqualTo("82,5");
        await Assert.That(vm.BmiText).IsEqualTo("BMI 25,5");
        await Assert.That(vm.BmiNote).IsEqualTo("Dies ist keine Diagnose.");
    }

    [Test]
    public async Task Bmi_IsHidden_WhenAValueIsMissing_AndHasNoCategory()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, null));
        await vm.InitializeCommand.ExecuteAsync(null);
        await Assert.That(vm.BmiText).IsNull();

        vm.WeightText = "150";
        await Assert.That(vm.BmiText).IsEqualTo("BMI 46,3");
        await Assert.That(vm.BmiText!).DoesNotContain("adipös");
    }

    [Test]
    [Arguments("82,5")]
    [Arguments("82.5")]
    public async Task Save_AcceptsBothDecimalSeparators_AndConfirms(string input)
    {
        var vm = await CreateAsync();
        vm.AgeText = "42";
        vm.HeightText = "180";
        vm.WeightText = input;

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(_api.Puts[0]).IsEqualTo(new ProfileDto(42, 180, 82.5m));
        await Assert.That(vm.StatusMessage).IsEqualTo("Das Profil wurde gespeichert.");
        await Assert.That(_dialogs.Announcements).Contains("Das Profil wurde gespeichert.");
    }

    [Test]
    public async Task EmptyFields_AreSentAsNull()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, 82.5m));
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.WeightText = "";

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(_api.Puts[0]).IsEqualTo(new ProfileDto(42, 180, null));
    }

    [Test]
    public async Task NonNumeric_ShowsMessage_WithoutRequest()
    {
        var vm = await CreateAsync();
        vm.HeightText = "abc";

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Bitte gib gültige Zahlen ein." });
        await Assert.That(_api.Puts).IsEmpty();
        await Assert.That(_dialogs.Announcements).Contains("Bitte gib gültige Zahlen ein.");
    }

    [Test]
    public async Task ServerValidation_IsShown_AndInputKept()
    {
        var vm = await CreateAsync();
        _api.PutFailWith = ApiExceptions.Validation("heightCm", "Die Größe muss zwischen 50 und 250 cm liegen.");
        vm.HeightText = "20";

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Die Größe muss zwischen 50 und 250 cm liegen." });
        await Assert.That(vm.HeightText).IsEqualTo("20");
        await Assert.That(vm.StatusMessage).IsNull();
    }

    [Test]
    public async Task Delete_ConfirmedClearsFields_AndDeclinedDoesNothing()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, 82.5m));
        await vm.InitializeCommand.ExecuteAsync(null);
        _dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);
        await Assert.That(_api.Deletes).IsEqualTo(0);

        _dialogs.ConfirmResult = true;
        await vm.DeleteCommand.ExecuteAsync(null);

        await Assert.That(_api.Deletes).IsEqualTo(1);
        await Assert.That(vm.AgeText).IsEqualTo("");
        await Assert.That(vm.BmiText).IsNull();
        await Assert.That(vm.StatusMessage).IsEqualTo("Die Profilangaben wurden gelöscht.");
    }

    [Test]
    public async Task Unauthorized_SignsOut()
    {
        var vm = await CreateAsync();
        _api.PutFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task LoadFails_ShowsGenericMessage()
    {
        var vm = await CreateAsync();
        _api.GetFailWith = new HttpRequestException("secret");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Etwas ist schiefgegangen. Bitte versuche es später erneut." });
    }

    [Test]
    public async Task Theme_ReflectsStoredChoice_AppliesNewChoice_AndNeverTouchesTheProfileApi()
    {
        _settings.Items["bp.theme"] = "light";
        var vm = await CreateAsync();
        await Assert.That(vm.IsThemeLight).IsTrue();
        await Assert.That(vm.IsThemeSystem).IsFalse();

        vm.IsThemeDark = true;

        await Assert.That(_applier.Applied).IsEquivalentTo(new[] { "dark" });
        await Assert.That(vm.IsThemeLight).IsFalse();
        await Assert.That(vm.IsThemeDark).IsTrue();
        await Assert.That(_api.Puts).IsEmpty();
        await Assert.That(_dialogs.Announcements).Contains("Darstellung: Dunkel");
    }

    [Test]
    public async Task Language_ReflectsStoredChoice_AppliesNewChoice_AnnouncesIt_AndNeverTouchesTheProfileApi()
    {
        var vm = await CreateAsync();
        await Assert.That(vm.IsLanguageSystem).IsTrue();

        vm.IsLanguageEnglish = true;

        await Assert.That(_languageApplier.Applied.Select(c => c.Name)).IsEquivalentTo(new[] { "en" });
        await Assert.That(_settings.Items["bp.language"]).IsEqualTo("en");
        await Assert.That(vm.IsLanguageEnglish).IsTrue();
        await Assert.That(vm.IsLanguageSystem).IsFalse();
        await Assert.That(_api.Puts).IsEmpty();
        await Assert.That(_dialogs.Announcements).Contains("Language: English");
    }

    [Test]
    public async Task Language_StoredChoice_IsShownAsSelected()
    {
        _settings.Items["bp.language"] = "en";
        var vm = await CreateAsync();

        await Assert.That(vm.IsLanguageEnglish).IsTrue();
        await Assert.That(_localizer.Culture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task Language_SelectingTheSameChoiceAgain_DoesNothing()
    {
        var vm = await CreateAsync();
        vm.IsLanguageGerman = true;
        _languageApplier.Applied.Clear();
        _dialogs.Announcements.Clear();

        vm.IsLanguageGerman = true;
        vm.IsLanguageEnglish = false;

        await Assert.That(_languageApplier.Applied).IsEmpty();
        await Assert.That(_dialogs.Announcements).IsEmpty();
    }

    [Test]
    public async Task Language_SwitchingChangesTheTextsAndNumberFormatAtOnce()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, 82.5m));
        await vm.InitializeCommand.ExecuteAsync(null);
        await Assert.That(vm.WeightText).IsEqualTo("82,5");
        await Assert.That(vm.BmiText).Contains("25,5");

        vm.IsLanguageEnglish = true;

        await Assert.That(vm.BmiText).Contains("25.5");
        await Assert.That(vm.BmiNote).Contains("not a diagnosis");
        // Eingaben mit Komma bleiben gültig; ungespeicherte Texte werden nicht verändert.
        await Assert.That(vm.WeightText).IsEqualTo("82,5");
    }

    [Test]
    public async Task English_UsesPointAndEnglishTexts()
    {
        var vm = await CreateAsync(new ProfileDto(42, 180, 82.5m), culture: "en");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.WeightText).IsEqualTo("82.5");
        await Assert.That(vm.BmiNote).IsEqualTo("This is not a diagnosis.");
    }
}
