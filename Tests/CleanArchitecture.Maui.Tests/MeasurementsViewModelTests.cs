using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class MeasurementsViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeBloodPressureApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();
    private SessionService _session = null!;

    private async Task<MeasurementsViewModel> CreateAsync(int items = 0, string culture = "de", bool signedIn = true)
    {
        _api.Seed(items);
        _session = new SessionService(_store, _auth, _time);
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        return new MeasurementsViewModel(_api, _session, _nav, _dialogs, new ResxLocalizer(culture == "de" ? Culture.De : Culture.En), _time);
    }

    private static void Fill(MeasurementsViewModel vm, string sys = "120", string dia = "80", string pulse = "65", string oxygen = "98")
    {
        vm.SystolicText = sys;
        vm.DiastolicText = dia;
        vm.PulseText = pulse;
        vm.OxygenText = oxygen;
    }

    [Test]
    public async Task NotSignedIn_NavigatesToLogin_WithoutApiCall()
    {
        var vm = await CreateAsync(signedIn: false);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_api.ListCalls).IsEmpty();
    }

    [Test]
    public async Task Initialize_LoadsFirstPage_NewestFirst_WithPageSizeTwenty()
    {
        var vm = await CreateAsync(25);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items.Count).IsEqualTo(20);
        await Assert.That(vm.HasMore).IsTrue();
        await Assert.That(_api.ListCalls[0]).IsEqualTo((1, 20, "-dateTime"));
    }

    [Test]
    public async Task LoadMore_AppendsNextPage_UntilEnd()
    {
        var vm = await CreateAsync(25);
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        await Assert.That(vm.Items.Count).IsEqualTo(25);
        await Assert.That(vm.HasMore).IsFalse();
        await Assert.That(_api.ListCalls.Last().Page).IsEqualTo(2);
    }

    [Test]
    public async Task Empty_ShowsEmptyState()
    {
        var vm = await CreateAsync(0);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Item_ShowsCategoryAsLocalizedText()
    {
        var vm = await CreateAsync();
        _api.Items.Add(FakeBloodPressureApi.Make(1, 165, 100, category: "Grade2"));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].CategoryText).IsEqualTo("Hypertonie Grad 2");
        await Assert.That(vm.Items[0].CategoryLevel).IsEqualTo(CategoryLevel.High);
    }

    [Test]
    public async Task Create_Valid_SendsUtcTimeFromLocalInput_AndReloads()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);
        Fill(vm);

        await vm.CreateCommand.ExecuteAsync(null);

        await Assert.That(_api.Created.Count).IsEqualTo(1);
        await Assert.That(_api.Created[0].Systolisch).IsEqualTo(120);
        await Assert.That(_api.Created[0].DateTime).IsEqualTo(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        await Assert.That(vm.Items.Count).IsEqualTo(1);
        await Assert.That(vm.SystolicText).IsEqualTo("");
        await Assert.That(vm.FormErrors).IsEmpty();
    }

    [Test]
    public async Task Create_MissingOrNonNumeric_ShowsMessage_WithoutApiCall()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);
        Fill(vm, sys: "abc");

        await vm.CreateCommand.ExecuteAsync(null);

        await Assert.That(vm.FormErrors).IsEquivalentTo(new[] { "Bitte fülle alle Felder mit Zahlen aus." });
        await Assert.That(_api.Created).IsEmpty();
        await Assert.That(_dialogs.Announcements).Contains("Bitte fülle alle Felder mit Zahlen aus.");
    }

    [Test]
    public async Task Create_ServerValidation_ShowsFieldMessages()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);
        _api.CreateFailWith = ApiExceptions.Validation("systolisch", "Systolisch muss zwischen 0 und 500 liegen.");
        Fill(vm, sys: "999");

        await vm.CreateCommand.ExecuteAsync(null);

        await Assert.That(vm.FormErrors).IsEquivalentTo(new[] { "Systolisch muss zwischen 0 und 500 liegen." });
        await Assert.That(vm.SystolicText).IsEqualTo("999");
    }

    [Test]
    public async Task Delete_Confirmed_CallsApiWithETag_AndReloads()
    {
        var vm = await CreateAsync(2);
        await vm.InitializeCommand.ExecuteAsync(null);
        var item = vm.Items[0];

        await vm.DeleteCommand.ExecuteAsync(item);

        await Assert.That(_api.Deleted).IsEquivalentTo(new[] { (item.Id, (string?)item.ETag) });
        await Assert.That(vm.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Delete_NotConfirmed_DoesNothing()
    {
        var vm = await CreateAsync(2);
        await vm.InitializeCommand.ExecuteAsync(null);
        _dialogs.ConfirmResult = false;

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);

        await Assert.That(_api.Deleted).IsEmpty();
        await Assert.That(vm.Items.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(HttpStatusCode.PreconditionFailed)]
    [Arguments(HttpStatusCode.Conflict)]
    public async Task Delete_Conflict_ShowsHint_AndReloads(HttpStatusCode status)
    {
        var vm = await CreateAsync(2);
        await vm.InitializeCommand.ExecuteAsync(null);
        _api.DeleteFailWith = ApiExceptions.Create(status);
        var calls = _api.ListCalls.Count;

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);

        await Assert.That(vm.Message).Contains("geändert");
        await Assert.That(_api.ListCalls.Count).IsGreaterThan(calls);
    }

    [Test]
    public async Task Unauthorized_SignsOut_AndNavigatesToLogin()
    {
        var vm = await CreateAsync(1);
        _api.ListFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
        await Assert.That(_store.Items).IsEmpty();
    }

    [Test]
    public async Task LoadFails_ShowsGenericMessage()
    {
        var vm = await CreateAsync(1);
        _api.ListFailWith = new HttpRequestException("db password=secret");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
    }

    [Test]
    public async Task EnglishCulture_UsesEnglishTexts()
    {
        var vm = await CreateAsync(culture: "en");
        _api.Items.Add(FakeBloodPressureApi.Make(1, 165, 100, category: "Grade2"));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].CategoryText).IsEqualTo("Hypertension grade 2");
    }

    [Test]
    public async Task Logout_ClearsSession_AndNavigatesToLogin()
    {
        var vm = await CreateAsync();

        await vm.LogoutCommand.ExecuteAsync(null);

        await Assert.That(_session.IsAuthenticated).IsFalse();
        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
    }
}
